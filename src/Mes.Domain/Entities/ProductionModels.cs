namespace Mes.Domain.Entities;

public sealed record ParameterSpec(string Code, decimal? LowerLimit, decimal? UpperLimit, string Unit, bool IsBlocking)
{
    public bool IsInSpec(decimal value)
    {
        return (!LowerLimit.HasValue || value >= LowerLimit.Value)
            && (!UpperLimit.HasValue || value <= UpperLimit.Value);
    }
}

public sealed record OperationDefinition(
    string Code,
    string Name,
    int Sequence,
    IReadOnlyCollection<string> RequiredMaterialCodes,
    IReadOnlyCollection<ParameterSpec> ParameterSpecs,
    bool RequiresInspection);

public sealed record ProcessRoute(string Code, string Version, IReadOnlyList<OperationDefinition> Operations)
{
    public OperationDefinition FirstOperation => Operations.OrderBy(operation => operation.Sequence).First();

    public OperationDefinition? GetOperation(string operationCode)
    {
        return Operations.FirstOrDefault(operation => operation.Code == operationCode);
    }

    public OperationDefinition? GetNextOperation(string operationCode)
    {
        var current = GetOperation(operationCode);
        return current is null
            ? null
            : Operations.OrderBy(operation => operation.Sequence)
                .FirstOrDefault(operation => operation.Sequence > current.Sequence);
    }
}

public sealed class ProductionOrder
{
    public ProductionOrder(string orderNo, string productCode, string routeCode, string routeVersion, int plannedQuantity, string lineCode, DateOnly dueDate)
    {
        if (plannedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannedQuantity), "Planned quantity must be greater than zero.");
        }

        OrderNo = orderNo;
        ProductCode = productCode;
        RouteCode = routeCode;
        RouteVersion = routeVersion;
        PlannedQuantity = plannedQuantity;
        LineCode = lineCode;
        DueDate = dueDate;
    }

    public string OrderNo { get; }
    public string ProductCode { get; }
    public string RouteCode { get; }
    public string RouteVersion { get; }
    public int PlannedQuantity { get; }
    public string LineCode { get; }
    public DateOnly DueDate { get; }
    public int GoodQuantity { get; private set; }
    public int ScrapQuantity { get; private set; }
    public WorkOrderStatus Status { get; private set; } = WorkOrderStatus.Draft;

    public void Release()
    {
        EnsureStatus(WorkOrderStatus.Draft);
        Status = WorkOrderStatus.Released;
    }

    public void MarkInProgress()
    {
        if (Status == WorkOrderStatus.Released || Status == WorkOrderStatus.Paused)
        {
            Status = WorkOrderStatus.InProgress;
            return;
        }

        EnsureStatus(WorkOrderStatus.InProgress);
    }

    public void CompleteUnit()
    {
        GoodQuantity++;
        if (GoodQuantity + ScrapQuantity >= PlannedQuantity)
        {
            Status = WorkOrderStatus.Completed;
        }
    }

    public void ScrapUnit()
    {
        ScrapQuantity++;
        if (GoodQuantity + ScrapQuantity >= PlannedQuantity)
        {
            Status = WorkOrderStatus.Completed;
        }
    }

    private void EnsureStatus(WorkOrderStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Order {OrderNo} status {Status} cannot perform this action; expected {expected}.");
        }
    }
}
