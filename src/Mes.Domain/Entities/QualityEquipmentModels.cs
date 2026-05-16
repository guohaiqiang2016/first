namespace Mes.Domain.Entities;

public sealed record InspectionTask(
    string TaskNo,
    string SerialNo,
    string OrderNo,
    string OperationCode,
    string InspectionType,
    InspectionTaskStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt = null)
{
    public InspectionTask Complete(DateTimeOffset completedAt)
    {
        if (Status != InspectionTaskStatus.Pending)
        {
            throw new InvalidOperationException($"Inspection task {TaskNo} is not pending.");
        }

        return this with { Status = InspectionTaskStatus.Completed, CompletedAt = completedAt };
    }
}

public sealed record Nonconformance(
    string NcNo,
    string SerialNo,
    string OrderNo,
    string OperationCode,
    string DefectCode,
    string Description,
    NonconformanceStatus Status,
    NonconformanceDisposition? Disposition,
    string? ResponsibleDepartment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DisposedAt = null)
{
    public Nonconformance Dispose(NonconformanceDisposition disposition, string responsibleDepartment, DateTimeOffset disposedAt)
    {
        if (Status != NonconformanceStatus.PendingReview)
        {
            throw new InvalidOperationException($"Nonconformance {NcNo} is not pending review.");
        }

        return this with
        {
            Status = NonconformanceStatus.Disposed,
            Disposition = disposition,
            ResponsibleDepartment = responsibleDepartment,
            DisposedAt = disposedAt
        };
    }
}

public sealed class Equipment
{
    private readonly List<EquipmentSample> _samples = [];
    private readonly List<EquipmentAlarm> _alarms = [];

    public Equipment(string equipmentCode, string name, string lineCode)
    {
        EquipmentCode = equipmentCode;
        Name = name;
        LineCode = lineCode;
    }

    public string EquipmentCode { get; }
    public string Name { get; }
    public string LineCode { get; }
    public EquipmentStatus Status { get; private set; } = EquipmentStatus.Standby;
    public IReadOnlyCollection<EquipmentSample> Samples => _samples;
    public IReadOnlyCollection<EquipmentAlarm> Alarms => _alarms;

    public bool CanStartProduction => Status is EquipmentStatus.Running or EquipmentStatus.Standby;

    public void ChangeStatus(EquipmentStatus status)
    {
        Status = status;
    }

    public void AddSample(EquipmentSample sample)
    {
        _samples.Add(sample);
    }

    public void AddAlarm(EquipmentAlarm alarm)
    {
        _alarms.Add(alarm);
        Status = EquipmentStatus.Fault;
    }
}

public sealed record EquipmentSample(
    string EquipmentCode,
    string TagCode,
    decimal Value,
    string Unit,
    DateTimeOffset RecordedAt,
    string? OrderNo,
    string? SerialNo,
    string? OperationCode);

public sealed record EquipmentAlarm(
    string AlarmNo,
    string EquipmentCode,
    string AlarmCode,
    string Message,
    DateTimeOffset RaisedAt,
    bool IsClosed = false,
    DateTimeOffset? ClosedAt = null)
{
    public EquipmentAlarm Close(DateTimeOffset closedAt)
    {
        return this with { IsClosed = true, ClosedAt = closedAt };
    }
}

public sealed record ProductionDashboard(
    int OrderCount,
    int ReleasedOrderCount,
    int InProgressOrderCount,
    int CompletedOrderCount,
    int PlannedQuantity,
    int GoodQuantity,
    int ScrapQuantity,
    decimal PlanCompletionRate,
    decimal FirstPassYield,
    int OpenNonconformanceCount,
    int OpenEquipmentAlarmCount);
