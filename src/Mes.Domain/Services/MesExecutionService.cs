using Mes.Domain.Entities;
using Mes.Domain.Repositories;

namespace Mes.Domain.Services;

public sealed class MesExecutionService(IMesRepository repository)
{
    public ProductionOrder CreateOrder(string orderNo, string productCode, string routeCode, string routeVersion, int plannedQuantity, string lineCode, DateOnly dueDate)
    {
        repository.GetRoute(routeCode, routeVersion);
        var order = new ProductionOrder(orderNo, productCode, routeCode, routeVersion, plannedQuantity, lineCode, dueDate);
        repository.AddOrder(order);
        return order;
    }

    public SerialUnit ReleaseSerial(string orderNo, string serialNo)
    {
        var order = repository.GetOrder(orderNo);
        if (order.Status == WorkOrderStatus.Draft)
        {
            order.Release();
        }

        var route = repository.GetRoute(order.RouteCode, order.RouteVersion);
        var unit = new SerialUnit(serialNo, order.OrderNo, order.ProductCode, route.FirstOperation.Code);
        repository.AddSerialUnit(unit);
        return unit;
    }

    public Equipment RegisterEquipment(string equipmentCode, string name, string lineCode)
    {
        var equipment = new Equipment(equipmentCode, name, lineCode);
        repository.AddEquipment(equipment);
        return equipment;
    }

    public void UpdateEquipmentStatus(string equipmentCode, EquipmentStatus status)
    {
        repository.GetEquipment(equipmentCode).ChangeStatus(status);
    }

    public void RecordEquipmentSample(string equipmentCode, string tagCode, decimal value, string unit, string? orderNo, string? serialNo, string? operationCode, DateTimeOffset now)
    {
        repository.GetEquipment(equipmentCode).AddSample(new EquipmentSample(equipmentCode, tagCode, value, unit, now, orderNo, serialNo, operationCode));
    }

    public EquipmentAlarm RaiseEquipmentAlarm(string equipmentCode, string alarmCode, string message, DateTimeOffset now)
    {
        var alarm = new EquipmentAlarm(NextId("ALM"), equipmentCode, alarmCode, message, now);
        repository.GetEquipment(equipmentCode).AddAlarm(alarm);
        return alarm;
    }

    public void StartOperation(string serialNo, string operationCode, string operatorId, string workstationCode, string equipmentCode, DateTimeOffset now)
    {
        var unit = repository.GetSerialUnit(serialNo);
        var order = repository.GetOrder(unit.OrderNo);
        var equipment = repository.GetEquipment(equipmentCode);
        if (!equipment.CanStartProduction)
        {
            throw new InvalidOperationException($"Equipment {equipmentCode} status {equipment.Status} cannot start production.");
        }

        order.MarkInProgress();
        equipment.ChangeStatus(EquipmentStatus.Running);
        unit.StartOperation(operationCode, operatorId, workstationCode, equipmentCode, now);
    }

    public void BindMaterial(string serialNo, string operationCode, string materialCode, string lotNo, decimal quantity, DateTimeOffset now)
    {
        var unit = repository.GetSerialUnit(serialNo);
        var operation = GetCurrentOperation(unit, operationCode);
        if (!operation.RequiredMaterialCodes.Contains(materialCode, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Material {materialCode} is not allowed for operation {operationCode}.");
        }

        unit.BindMaterial(materialCode, lotNo, quantity, now);
    }

    public void RecordParameter(string serialNo, string operationCode, string tagCode, decimal value, DateTimeOffset now)
    {
        var unit = repository.GetSerialUnit(serialNo);
        var operation = GetCurrentOperation(unit, operationCode);
        var spec = operation.ParameterSpecs.FirstOrDefault(parameter => string.Equals(parameter.Code, tagCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Parameter {tagCode} is not configured for operation {operationCode}.");

        unit.RecordParameter(operationCode, spec, value, now);
    }

    public InspectionTask? CompleteOperation(string serialNo, string operationCode, DateTimeOffset now)
    {
        var unit = repository.GetSerialUnit(serialNo);
        var order = repository.GetOrder(unit.OrderNo);
        var route = repository.GetRoute(order.RouteCode, order.RouteVersion);
        var operation = GetCurrentOperation(unit, operationCode);

        EnsureRequiredMaterials(unit, operation);
        EnsureBlockingParameters(unit, operation);

        var nextOperation = route.GetNextOperation(operationCode);
        unit.CompleteOperation(operationCode, nextOperation?.Code, operation.RequiresInspection, now);
        if (unit.Status == UnitStatus.Completed)
        {
            order.CompleteUnit();
        }

        if (!operation.RequiresInspection)
        {
            return null;
        }

        var task = new InspectionTask(NextId("IQC"), unit.SerialNo, unit.OrderNo, operation.Code, "工序检验", InspectionTaskStatus.Pending, now);
        repository.AddInspectionTask(task);
        return task;
    }

    public Nonconformance? RecordInspection(string serialNo, string operationCode, string itemCode, decimal? numericValue, string? textValue, InspectionJudgement judgement, string inspectorId, DateTimeOffset now)
    {
        var unit = repository.GetSerialUnit(serialNo);
        var order = repository.GetOrder(unit.OrderNo);
        var route = repository.GetRoute(order.RouteCode, order.RouteVersion);
        var operation = route.GetOperation(operationCode) ?? throw new InvalidOperationException($"Operation {operationCode} is not configured.");
        if (!operation.RequiresInspection)
        {
            throw new InvalidOperationException($"Operation {operationCode} does not require inspection.");
        }

        var pendingTask = repository.InspectionTasks
            .Where(task => task.SerialNo == serialNo && task.OperationCode == operationCode && task.Status == InspectionTaskStatus.Pending)
            .OrderBy(task => task.CreatedAt)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No pending inspection task exists for serial {serialNo} operation {operationCode}.");

        var nextOperation = route.GetNextOperation(operationCode);
        var result = new InspectionResult(operationCode, itemCode, numericValue, textValue, judgement, inspectorId, now);
        unit.RecordInspection(result, nextOperation?.Code);
        repository.UpdateInspectionTask(pendingTask.Complete(now));

        if (unit.Status == UnitStatus.Completed)
        {
            order.CompleteUnit();
        }

        if (judgement == InspectionJudgement.Pass)
        {
            return null;
        }

        var nc = new Nonconformance(NextId("NC"), unit.SerialNo, unit.OrderNo, operationCode, itemCode, textValue ?? "Inspection failed", NonconformanceStatus.PendingReview, null, null, now);
        repository.AddNonconformance(nc);
        return nc;
    }

    public Nonconformance DisposeNonconformance(string ncNo, NonconformanceDisposition disposition, string responsibleDepartment, DateTimeOffset now)
    {
        var nc = repository.GetNonconformance(ncNo);
        var updated = nc.Dispose(disposition, responsibleDepartment, now);
        repository.UpdateNonconformance(updated);

        var unit = repository.GetSerialUnit(nc.SerialNo);
        var order = repository.GetOrder(unit.OrderNo);
        switch (disposition)
        {
            case NonconformanceDisposition.Rework:
            case NonconformanceDisposition.Repair:
            case NonconformanceDisposition.ReturnToPreviousOperation:
                unit.StartRework(nc.OperationCode);
                break;
            case NonconformanceDisposition.Scrap:
                unit.Scrap();
                order.ScrapUnit();
                break;
            case NonconformanceDisposition.Concession:
                unit.MarkNonconforming();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Unsupported disposition.");
        }

        return updated;
    }

    public TraceReport Trace(string serialNo)
    {
        var unit = repository.GetSerialUnit(serialNo);
        return new TraceReport(
            unit.SerialNo,
            unit.OrderNo,
            unit.ProductCode,
            unit.Status,
            unit.Materials.OrderBy(material => material.BoundAt).ToList(),
            unit.Parameters.OrderBy(parameter => parameter.RecordedAt).ToList(),
            unit.Operations.OrderBy(operation => operation.StartedAt).ToList(),
            unit.Inspections.OrderBy(inspection => inspection.InspectedAt).ToList(),
            repository.InspectionTasks.Where(task => task.SerialNo == serialNo).OrderBy(task => task.CreatedAt).ToList(),
            repository.Nonconformances.Where(nc => nc.SerialNo == serialNo).OrderBy(nc => nc.CreatedAt).ToList());
    }

    public IReadOnlyCollection<TraceReport> TraceByMaterialLot(string materialLot)
    {
        return repository.SerialUnits
            .Where(unit => unit.Materials.Any(material => string.Equals(material.LotNo, materialLot, StringComparison.OrdinalIgnoreCase)))
            .Select(unit => Trace(unit.SerialNo))
            .OrderBy(trace => trace.SerialNo)
            .ToList();
    }

    public ProductionDashboard GetDashboard()
    {
        var orders = repository.Orders.ToList();
        var serials = repository.SerialUnits.ToList();
        var plannedQuantity = orders.Sum(order => order.PlannedQuantity);
        var goodQuantity = orders.Sum(order => order.GoodQuantity);
        var scrapQuantity = orders.Sum(order => order.ScrapQuantity);
        var inspectedCount = serials.Count(unit => unit.Inspections.Count > 0);
        var firstPassCount = serials.Count(unit => unit.Inspections.Count > 0 && unit.Inspections.First().Judgement == InspectionJudgement.Pass);

        return new ProductionDashboard(
            orders.Count,
            orders.Count(order => order.Status == WorkOrderStatus.Released),
            orders.Count(order => order.Status == WorkOrderStatus.InProgress),
            orders.Count(order => order.Status == WorkOrderStatus.Completed),
            plannedQuantity,
            goodQuantity,
            scrapQuantity,
            plannedQuantity == 0 ? 0m : Math.Round((decimal)(goodQuantity + scrapQuantity) / plannedQuantity, 4),
            inspectedCount == 0 ? 0m : Math.Round((decimal)firstPassCount / inspectedCount, 4),
            repository.Nonconformances.Count(nc => nc.Status != NonconformanceStatus.Closed),
            repository.Equipment.SelectMany(equipment => equipment.Alarms).Count(alarm => !alarm.IsClosed));
    }

    private OperationDefinition GetCurrentOperation(SerialUnit unit, string operationCode)
    {
        var order = repository.GetOrder(unit.OrderNo);
        var route = repository.GetRoute(order.RouteCode, order.RouteVersion);
        var operation = route.GetOperation(operationCode) ?? throw new InvalidOperationException($"Operation {operationCode} is not configured.");
        if (unit.CurrentOperationCode != operationCode)
        {
            throw new InvalidOperationException($"Serial {unit.SerialNo} current operation is {unit.CurrentOperationCode}, not {operationCode}.");
        }

        return operation;
    }

    private static void EnsureRequiredMaterials(SerialUnit unit, OperationDefinition operation)
    {
        var missingMaterials = operation.RequiredMaterialCodes
            .Where(required => unit.Materials.All(bound => !string.Equals(bound.MaterialCode, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingMaterials.Count > 0)
        {
            throw new InvalidOperationException($"Missing required materials for operation {operation.Code}: {string.Join(", ", missingMaterials)}.");
        }
    }

    private static void EnsureBlockingParameters(SerialUnit unit, OperationDefinition operation)
    {
        foreach (var spec in operation.ParameterSpecs.Where(parameter => parameter.IsBlocking))
        {
            var records = unit.Parameters
                .Where(parameter => parameter.OperationCode == operation.Code && string.Equals(parameter.TagCode, spec.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (records.Count == 0)
            {
                throw new InvalidOperationException($"Missing blocking parameter {spec.Code} for operation {operation.Code}.");
            }

            if (records.Any(parameter => !parameter.IsInSpec))
            {
                throw new InvalidOperationException($"Blocking parameter {spec.Code} is out of specification for operation {operation.Code}.");
            }
        }
    }

    private static string NextId(string prefix)
    {
        return $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 1 + 17 + 1 + 12, prefix.Length + 1 + 17 + 1 + 32)];
    }
}

public sealed record TraceReport(
    string SerialNo,
    string OrderNo,
    string ProductCode,
    UnitStatus Status,
    IReadOnlyCollection<MaterialBinding> Materials,
    IReadOnlyCollection<ParameterRecord> Parameters,
    IReadOnlyCollection<OperationRecord> Operations,
    IReadOnlyCollection<InspectionResult> Inspections,
    IReadOnlyCollection<InspectionTask> InspectionTasks,
    IReadOnlyCollection<Nonconformance> Nonconformances);
