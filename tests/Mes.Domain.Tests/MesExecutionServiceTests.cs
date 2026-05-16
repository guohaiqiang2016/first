using Mes.Domain.Entities;
using Mes.Domain.Repositories;
using Mes.Domain.Services;
using Xunit;

namespace Mes.Domain.Tests;

public sealed class MesExecutionServiceTests
{
    [Fact]
    public void CompleteOperation_Blocks_WhenRequiredMaterialIsMissing()
    {
        var (service, _) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-001", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-001", "S-001");
        service.StartOperation("S-001", "WINDING", "OP-01", "WND-01", "EQ-WND-01", now);
        service.RecordParameter("S-001", "WINDING", "TENSION", 12m, now);
        service.RecordParameter("S-001", "WINDING", "TURNS", 48m, now);

        var error = Assert.Throws<InvalidOperationException>(() => service.CompleteOperation("S-001", "WINDING", now));
        Assert.Contains("Missing required materials", error.Message);
    }

    [Fact]
    public void CompleteOperation_Blocks_WhenBlockingParameterIsOutOfSpec()
    {
        var (service, _) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-002", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-002", "S-002");
        service.StartOperation("S-002", "WINDING", "OP-01", "WND-01", "EQ-WND-01", now);
        service.BindMaterial("S-002", "WINDING", "CU-WIRE-001", "CU-LOT-001", 0.45m, now);
        service.RecordParameter("S-002", "WINDING", "TENSION", 18m, now);
        service.RecordParameter("S-002", "WINDING", "TURNS", 48m, now);

        var error = Assert.Throws<InvalidOperationException>(() => service.CompleteOperation("S-002", "WINDING", now));
        Assert.Contains("out of specification", error.Message);
    }

    [Fact]
    public void Trace_ReturnsMaterialParameterOperationAndInspectionHistory()
    {
        var (service, _) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-003", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-003", "S-003");

        service.StartOperation("S-003", "WINDING", "OP-01", "WND-01", "EQ-WND-01", now);
        service.BindMaterial("S-003", "WINDING", "CU-WIRE-001", "CU-LOT-001", 0.45m, now);
        service.RecordParameter("S-003", "WINDING", "TENSION", 12m, now);
        service.RecordParameter("S-003", "WINDING", "TURNS", 48m, now);
        service.CompleteOperation("S-003", "WINDING", now);

        service.StartOperation("S-003", "EOL_TEST", "OP-02", "EOL-01", "EQ-EOL-01", now);
        service.RecordParameter("S-003", "EOL_TEST", "IR", 120m, now);
        service.CompleteOperation("S-003", "EOL_TEST", now);
        service.RecordInspection("S-003", "EOL_TEST", "IR", 120m, null, InspectionJudgement.Pass, "QA-01", now);

        var trace = service.Trace("S-003");
        Assert.Equal(UnitStatus.Completed, trace.Status);
        Assert.Contains(trace.Materials, material => material.MaterialCode == "CU-WIRE-001");
        Assert.Contains(trace.Parameters, parameter => parameter.TagCode == "TENSION" && parameter.IsInSpec);
        Assert.Contains(trace.Operations, operation => operation.OperationCode == "EOL_TEST");
        Assert.Contains(trace.Inspections, inspection => inspection.Judgement == InspectionJudgement.Pass);
    }

    [Fact]
    public void CompleteOperation_CreatesInspectionTask_ForInspectionOperation()
    {
        var (service, repository) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-004", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-004", "S-004");
        CompleteWinding(service, "S-004", now);
        service.StartOperation("S-004", "EOL_TEST", "OP-02", "EOL-01", "EQ-EOL-01", now);
        service.RecordParameter("S-004", "EOL_TEST", "IR", 120m, now);

        var task = service.CompleteOperation("S-004", "EOL_TEST", now);

        Assert.NotNull(task);
        Assert.Equal(InspectionTaskStatus.Pending, task.Status);
        Assert.Equal(UnitStatus.WaitingInspection, repository.GetSerialUnit("S-004").Status);
    }

    [Fact]
    public void FailedInspection_CreatesNonconformance_AndScrapDispositionUpdatesOrder()
    {
        var (service, repository) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-005", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-005", "S-005");
        CompleteWinding(service, "S-005", now);
        service.StartOperation("S-005", "EOL_TEST", "OP-02", "EOL-01", "EQ-EOL-01", now);
        service.RecordParameter("S-005", "EOL_TEST", "IR", 120m, now);
        service.CompleteOperation("S-005", "EOL_TEST", now);

        var nc = service.RecordInspection("S-005", "EOL_TEST", "IR", 80m, "绝缘电阻低", InspectionJudgement.Fail, "QA-01", now);
        Assert.NotNull(nc);
        Assert.Equal(UnitStatus.Nonconforming, repository.GetSerialUnit("S-005").Status);

        service.DisposeNonconformance(nc.NcNo, NonconformanceDisposition.Scrap, "质量部", now);

        Assert.Equal(UnitStatus.Scrapped, repository.GetSerialUnit("S-005").Status);
        Assert.Equal(1, repository.GetOrder("WO-005").ScrapQuantity);
    }

    [Fact]
    public void TraceByMaterialLot_ReturnsImpactedSerials()
    {
        var (service, _) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-006", "STATOR-A", "STATOR-A", "1.0", 2, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-006", "S-006-A");
        service.ReleaseSerial("WO-006", "S-006-B");
        service.StartOperation("S-006-A", "WINDING", "OP-01", "WND-01", "EQ-WND-01", now);
        service.BindMaterial("S-006-A", "WINDING", "CU-WIRE-001", "CU-LOT-SHARED", 0.45m, now);
        service.StartOperation("S-006-B", "WINDING", "OP-01", "WND-01", "EQ-WND-01", now);
        service.BindMaterial("S-006-B", "WINDING", "CU-WIRE-001", "CU-LOT-SHARED", 0.45m, now);

        var traces = service.TraceByMaterialLot("CU-LOT-SHARED");

        Assert.Equal(2, traces.Count);
        Assert.Contains(traces, trace => trace.SerialNo == "S-006-A");
        Assert.Contains(traces, trace => trace.SerialNo == "S-006-B");
    }

    [Fact]
    public void StartOperation_Blocks_WhenEquipmentIsFaulted()
    {
        var (service, _) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-007", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-007", "S-007");
        service.UpdateEquipmentStatus("EQ-WND-01", EquipmentStatus.Fault);

        var error = Assert.Throws<InvalidOperationException>(() => service.StartOperation("S-007", "WINDING", "OP-01", "WND-01", "EQ-WND-01", now));
        Assert.Contains("cannot start production", error.Message);
    }

    [Fact]
    public void Dashboard_ReturnsPlanQualityAndEquipmentSummary()
    {
        var (service, _) = CreateService();
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        service.CreateOrder("WO-008", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));
        service.ReleaseSerial("WO-008", "S-008");
        CompleteWinding(service, "S-008", now);
        service.StartOperation("S-008", "EOL_TEST", "OP-02", "EOL-01", "EQ-EOL-01", now);
        service.RecordParameter("S-008", "EOL_TEST", "IR", 120m, now);
        service.CompleteOperation("S-008", "EOL_TEST", now);
        service.RecordInspection("S-008", "EOL_TEST", "IR", 120m, null, InspectionJudgement.Pass, "QA-01", now);
        service.RaiseEquipmentAlarm("EQ-WND-01", "E001", "张力波动", now);

        var dashboard = service.GetDashboard();

        Assert.Equal(1, dashboard.CompletedOrderCount);
        Assert.Equal(1, dashboard.GoodQuantity);
        Assert.Equal(1m, dashboard.PlanCompletionRate);
        Assert.Equal(1m, dashboard.FirstPassYield);
        Assert.Equal(1, dashboard.OpenEquipmentAlarmCount);
    }

    [Fact]
    public void ReceiveProductionPlan_CreatesOrderAndQueuesWmsPickingRequest()
    {
        var (executionService, repository) = CreateService();
        var integrationService = new MesIntegrationService(repository, executionService);
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");

        var order = integrationService.ReceiveProductionPlan(new ErpProductionPlan("PLN-001", "STATOR-A", "STATOR-A", "1.0", 10, "L-STATOR-01", DateOnly.Parse("2026-05-20"), 1), now);

        Assert.Equal("WO-PLN-001", order.OrderNo);
        Assert.Contains(repository.IntegrationMessages, message => message.System == ExternalSystem.ERP && message.Direction == IntegrationDirection.Inbound && message.MessageType == "ProductionPlan");
        Assert.Contains(repository.IntegrationMessages, message => message.System == ExternalSystem.WMS && message.Direction == IntegrationDirection.Outbound && message.MessageType == "PickingRequest");
    }

    [Fact]
    public void QueueCompletion_CreatesPendingErpOutboxMessageAndMarksDelivered()
    {
        var (executionService, repository) = CreateService();
        var integrationService = new MesIntegrationService(repository, executionService);
        var now = DateTimeOffset.Parse("2026-05-16T08:00:00Z");
        executionService.CreateOrder("WO-009", "STATOR-A", "STATOR-A", "1.0", 1, "L-STATOR-01", DateOnly.Parse("2026-05-20"));

        var message = integrationService.QueueCompletion("WO-009", now);
        var outbox = integrationService.GetPendingOutbox(ExternalSystem.ERP);

        Assert.Contains(outbox, queued => queued.MessageId == message.MessageId);
        Assert.Contains("WO-009", message.PayloadJson);

        var delivered = integrationService.MarkDelivered(message.MessageId, now.AddMinutes(1));

        Assert.Equal(IntegrationMessageStatus.Delivered, delivered.Status);
        Assert.DoesNotContain(integrationService.GetPendingOutbox(ExternalSystem.ERP), queued => queued.MessageId == message.MessageId);
    }

    private static void CompleteWinding(MesExecutionService service, string serialNo, DateTimeOffset now)
    {
        service.StartOperation(serialNo, "WINDING", "OP-01", "WND-01", "EQ-WND-01", now);
        service.BindMaterial(serialNo, "WINDING", "CU-WIRE-001", "CU-LOT-001", 0.45m, now);
        service.RecordParameter(serialNo, "WINDING", "TENSION", 12m, now);
        service.RecordParameter(serialNo, "WINDING", "TURNS", 48m, now);
        service.CompleteOperation(serialNo, "WINDING", now);
    }

    private static (MesExecutionService Service, InMemoryMesRepository Repository) CreateService()
    {
        var repository = new InMemoryMesRepository();
        repository.AddRoute(new ProcessRoute("STATOR-A", "1.0", [
            new OperationDefinition("WINDING", "定子绕线", 10, ["CU-WIRE-001"], [new ParameterSpec("TENSION", 10m, 14m, "N", true), new ParameterSpec("TURNS", 48m, 48m, "turn", true)], false),
            new OperationDefinition("EOL_TEST", "定子终检", 20, [], [new ParameterSpec("IR", 100m, null, "MΩ", true)], true)
        ]));
        repository.AddEquipment(new Equipment("EQ-WND-01", "绕线机 01", "L-STATOR-01"));
        repository.AddEquipment(new Equipment("EQ-EOL-01", "终检台 01", "L-STATOR-01"));

        return (new MesExecutionService(repository), repository);
    }
}
