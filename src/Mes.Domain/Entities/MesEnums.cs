namespace Mes.Domain.Entities;

public enum WorkOrderStatus
{
    Draft,
    Released,
    InProgress,
    Paused,
    Completed,
    Closed
}

public enum UnitStatus
{
    Created,
    InProcess,
    WaitingInspection,
    Nonconforming,
    Rework,
    Completed,
    Scrapped
}

public enum InspectionJudgement
{
    Pass,
    Fail
}

public enum InspectionTaskStatus
{
    Pending,
    Completed,
    Cancelled
}

public enum EquipmentStatus
{
    Running,
    Standby,
    Fault,
    Maintenance,
    Offline
}

public enum NonconformanceStatus
{
    PendingReview,
    Disposed,
    Closed
}

public enum NonconformanceDisposition
{
    Rework,
    Repair,
    Scrap,
    Concession,
    ReturnToPreviousOperation
}

public enum ExternalSystem
{
    ERP,
    PLM,
    WMS,
    QMS,
    SCADA,
    EAM
}

public enum IntegrationDirection
{
    Inbound,
    Outbound
}

public enum IntegrationMessageStatus
{
    Pending,
    Delivered,
    Failed
}
