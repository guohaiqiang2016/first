using System.Text.Json;
using Mes.Domain.Entities;
using Mes.Domain.Repositories;

namespace Mes.Domain.Services;

public sealed class MesIntegrationService(IMesRepository repository, MesExecutionService executionService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProductionOrder ReceiveProductionPlan(ErpProductionPlan plan, DateTimeOffset now)
    {
        if (plan.PlannedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plan), "ERP production plan quantity must be greater than zero.");
        }

        var orderNo = $"WO-{plan.PlanNo}";
        var order = executionService.CreateOrder(orderNo, plan.ProductCode, plan.RouteCode, plan.RouteVersion, plan.PlannedQuantity, plan.LineCode, plan.DueDate);
        AddMessage(ExternalSystem.ERP, IntegrationDirection.Inbound, "ProductionPlan", plan.PlanNo, plan, now);
        QueuePickingRequest(order.OrderNo, now);
        return order;
    }

    public IntegrationMessage QueueCompletion(string orderNo, DateTimeOffset now)
    {
        var order = repository.GetOrder(orderNo);
        var payload = new
        {
            order.OrderNo,
            order.ProductCode,
            order.GoodQuantity,
            order.ScrapQuantity,
            order.Status,
            CompletedAt = now
        };
        return AddMessage(ExternalSystem.ERP, IntegrationDirection.Outbound, "OrderCompletion", order.OrderNo, payload, now);
    }

    public IntegrationMessage QueuePickingRequest(string orderNo, DateTimeOffset now)
    {
        var order = repository.GetOrder(orderNo);
        var route = repository.GetRoute(order.RouteCode, order.RouteVersion);
        var materialCodes = route.Operations.SelectMany(operation => operation.RequiredMaterialCodes).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        var payload = new
        {
            order.OrderNo,
            order.ProductCode,
            order.PlannedQuantity,
            order.LineCode,
            Materials = materialCodes.Select(code => new { MaterialCode = code, RequiredForOrder = true }).ToList()
        };
        return AddMessage(ExternalSystem.WMS, IntegrationDirection.Outbound, "PickingRequest", order.OrderNo, payload, now);
    }

    public IntegrationMessage QueueQualityNotice(string ncNo, DateTimeOffset now)
    {
        var nc = repository.GetNonconformance(ncNo);
        return AddMessage(ExternalSystem.QMS, IntegrationDirection.Outbound, "NonconformanceNotice", nc.NcNo, nc, now);
    }

    public IntegrationMessage QueueEquipmentAlarmNotice(string equipmentCode, string alarmNo, DateTimeOffset now)
    {
        var equipment = repository.GetEquipment(equipmentCode);
        var alarm = equipment.Alarms.First(alarm => alarm.AlarmNo == alarmNo);
        return AddMessage(ExternalSystem.EAM, IntegrationDirection.Outbound, "EquipmentAlarm", alarm.AlarmNo, alarm, now);
    }

    public IReadOnlyCollection<IntegrationMessage> GetPendingOutbox(ExternalSystem? system = null)
    {
        return repository.IntegrationMessages
            .Where(message => message.Direction == IntegrationDirection.Outbound && message.Status == IntegrationMessageStatus.Pending)
            .Where(message => system is null || message.System == system.Value)
            .OrderBy(message => message.CreatedAt)
            .ToList();
    }

    public IntegrationMessage MarkDelivered(string messageId, DateTimeOffset now)
    {
        var delivered = repository.GetIntegrationMessage(messageId).MarkDelivered(now);
        repository.UpdateIntegrationMessage(delivered);
        return delivered;
    }

    private IntegrationMessage AddMessage(ExternalSystem system, IntegrationDirection direction, string messageType, string businessKey, object payload, DateTimeOffset now)
    {
        var message = new IntegrationMessage(
            NextId("MSG"),
            system,
            direction,
            messageType,
            businessKey,
            JsonSerializer.Serialize(payload, JsonOptions),
            IntegrationMessageStatus.Pending,
            now);
        repository.AddIntegrationMessage(message);
        return message;
    }

    private static string NextId(string prefix)
    {
        return $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 1 + 17 + 1 + 12, prefix.Length + 1 + 17 + 1 + 32)];
    }
}
