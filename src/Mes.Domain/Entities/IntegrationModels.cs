namespace Mes.Domain.Entities;

public sealed record IntegrationMessage(
    string MessageId,
    ExternalSystem System,
    IntegrationDirection Direction,
    string MessageType,
    string BusinessKey,
    string PayloadJson,
    IntegrationMessageStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt = null,
    string? Error = null)
{
    public IntegrationMessage MarkDelivered(DateTimeOffset deliveredAt)
    {
        return this with { Status = IntegrationMessageStatus.Delivered, DeliveredAt = deliveredAt, Error = null };
    }

    public IntegrationMessage MarkFailed(string error)
    {
        return this with { Status = IntegrationMessageStatus.Failed, Error = error };
    }
}

public sealed record ErpProductionPlan(
    string PlanNo,
    string ProductCode,
    string RouteCode,
    string RouteVersion,
    int PlannedQuantity,
    string LineCode,
    DateOnly DueDate,
    int Priority);
