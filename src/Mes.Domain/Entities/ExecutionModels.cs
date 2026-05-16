namespace Mes.Domain.Entities;

public sealed record MaterialBinding(string MaterialCode, string LotNo, decimal Quantity, DateTimeOffset BoundAt);

public sealed record ParameterRecord(string OperationCode, string TagCode, decimal Value, string Unit, DateTimeOffset RecordedAt, bool IsInSpec);

public sealed record OperationRecord(
    string OperationCode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string OperatorId,
    string WorkstationCode,
    string EquipmentCode);

public sealed record InspectionResult(
    string OperationCode,
    string ItemCode,
    decimal? NumericValue,
    string? TextValue,
    InspectionJudgement Judgement,
    string InspectorId,
    DateTimeOffset InspectedAt);

public sealed class SerialUnit
{
    private readonly List<MaterialBinding> _materials = [];
    private readonly List<ParameterRecord> _parameters = [];
    private readonly List<OperationRecord> _operations = [];
    private readonly List<InspectionResult> _inspections = [];

    public SerialUnit(string serialNo, string orderNo, string productCode, string currentOperationCode)
    {
        SerialNo = serialNo;
        OrderNo = orderNo;
        ProductCode = productCode;
        CurrentOperationCode = currentOperationCode;
    }

    public string SerialNo { get; }
    public string OrderNo { get; }
    public string ProductCode { get; }
    public string CurrentOperationCode { get; private set; }
    public UnitStatus Status { get; private set; } = UnitStatus.Created;
    public IReadOnlyCollection<MaterialBinding> Materials => _materials;
    public IReadOnlyCollection<ParameterRecord> Parameters => _parameters;
    public IReadOnlyCollection<OperationRecord> Operations => _operations;
    public IReadOnlyCollection<InspectionResult> Inspections => _inspections;

    public void StartOperation(string operationCode, string operatorId, string workstationCode, string equipmentCode, DateTimeOffset now)
    {
        if (operationCode != CurrentOperationCode)
        {
            throw new InvalidOperationException($"Serial {SerialNo} must execute {CurrentOperationCode} before {operationCode}.");
        }

        if (_operations.Any(operation => operation.OperationCode == operationCode && operation.CompletedAt is null))
        {
            throw new InvalidOperationException($"Operation {operationCode} is already started for serial {SerialNo}.");
        }

        _operations.Add(new OperationRecord(operationCode, now, null, operatorId, workstationCode, equipmentCode));
        Status = UnitStatus.InProcess;
    }

    public void BindMaterial(string materialCode, string lotNo, decimal quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Material quantity must be greater than zero.");
        }

        _materials.Add(new MaterialBinding(materialCode, lotNo, quantity, now));
    }

    public void RecordParameter(string operationCode, ParameterSpec spec, decimal value, DateTimeOffset now)
    {
        _parameters.Add(new ParameterRecord(operationCode, spec.Code, value, spec.Unit, now, spec.IsInSpec(value)));
    }

    public void CompleteOperation(string operationCode, string? nextOperationCode, bool waitingInspection, DateTimeOffset now)
    {
        var index = _operations.FindIndex(operation => operation.OperationCode == operationCode && operation.CompletedAt is null);
        if (index < 0)
        {
            throw new InvalidOperationException($"Operation {operationCode} has not been started for serial {SerialNo}.");
        }

        var started = _operations[index];
        _operations[index] = started with { CompletedAt = now };

        if (waitingInspection)
        {
            Status = UnitStatus.WaitingInspection;
            return;
        }

        MoveToNext(nextOperationCode);
    }

    public void RecordInspection(InspectionResult result, string? nextOperationCode)
    {
        if (Status != UnitStatus.WaitingInspection)
        {
            throw new InvalidOperationException($"Serial {SerialNo} is not waiting for inspection.");
        }

        _inspections.Add(result);
        if (result.Judgement == InspectionJudgement.Fail)
        {
            Status = UnitStatus.Nonconforming;
            return;
        }

        MoveToNext(nextOperationCode);
    }

    public void MarkNonconforming()
    {
        Status = UnitStatus.Nonconforming;
    }

    public void StartRework(string operationCode)
    {
        CurrentOperationCode = operationCode;
        Status = UnitStatus.Rework;
    }

    public void Scrap()
    {
        Status = UnitStatus.Scrapped;
    }

    private void MoveToNext(string? nextOperationCode)
    {
        if (nextOperationCode is null)
        {
            Status = UnitStatus.Completed;
            return;
        }

        CurrentOperationCode = nextOperationCode;
        Status = UnitStatus.Created;
    }
}
