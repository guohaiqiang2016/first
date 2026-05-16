using Mes.Domain.Entities;
using Mes.Domain.Repositories;
using Mes.Domain.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IMesRepository, InMemoryMesRepository>();
builder.Services.AddSingleton<MesExecutionService>();
builder.Services.AddSingleton<MesIntegrationService>();

var app = builder.Build();
var repository = app.Services.GetRequiredService<IMesRepository>();
SeedRoutes(repository);
SeedEquipment(repository);

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "stator-rotor-mes" }));
app.MapGet("/api/dashboard", (MesExecutionService service) => Results.Ok(service.GetDashboard()));

app.MapPost("/api/integrations/erp/production-plans", (ErpProductionPlanRequest request, MesIntegrationService service) =>
{
    var plan = new ErpProductionPlan(request.PlanNo, request.ProductCode, request.RouteCode, request.RouteVersion, request.PlannedQuantity, request.LineCode, request.DueDate, request.Priority);
    var order = service.ReceiveProductionPlan(plan, DateTimeOffset.UtcNow);
    return Results.Created($"/api/orders/{order.OrderNo}", order);
});

app.MapGet("/api/integrations/outbox", (ExternalSystem? system, MesIntegrationService service) => Results.Ok(service.GetPendingOutbox(system)));

app.MapPost("/api/integrations/messages/{messageId}/delivered", (string messageId, MesIntegrationService service) =>
{
    var message = service.MarkDelivered(messageId, DateTimeOffset.UtcNow);
    return Results.Ok(message);
});

app.MapPost("/api/integrations/orders/{orderNo}/completion", (string orderNo, MesIntegrationService service) =>
{
    var message = service.QueueCompletion(orderNo, DateTimeOffset.UtcNow);
    return Results.Accepted(value: message);
});

app.MapPost("/api/integrations/orders/{orderNo}/picking-request", (string orderNo, MesIntegrationService service) =>
{
    var message = service.QueuePickingRequest(orderNo, DateTimeOffset.UtcNow);
    return Results.Accepted(value: message);
});

app.MapPost("/api/orders", (CreateOrderRequest request, MesExecutionService service) =>
{
    var order = service.CreateOrder(request.OrderNo, request.ProductCode, request.RouteCode, request.RouteVersion, request.PlannedQuantity, request.LineCode, request.DueDate);
    return Results.Created($"/api/orders/{order.OrderNo}", order);
});

app.MapPost("/api/orders/{orderNo}/serials", (string orderNo, ReleaseSerialRequest request, MesExecutionService service) =>
{
    var unit = service.ReleaseSerial(orderNo, request.SerialNo);
    return Results.Created($"/api/trace/{unit.SerialNo}", unit);
});

app.MapPost("/api/serials/{serialNo}/operations/{operationCode}/start", (string serialNo, string operationCode, StartOperationRequest request, MesExecutionService service) =>
{
    service.StartOperation(serialNo, operationCode, request.OperatorId, request.WorkstationCode, request.EquipmentCode, DateTimeOffset.UtcNow);
    return Results.Accepted();
});

app.MapPost("/api/serials/{serialNo}/operations/{operationCode}/materials", (string serialNo, string operationCode, BindMaterialRequest request, MesExecutionService service) =>
{
    service.BindMaterial(serialNo, operationCode, request.MaterialCode, request.LotNo, request.Quantity, DateTimeOffset.UtcNow);
    return Results.Accepted();
});

app.MapPost("/api/serials/{serialNo}/operations/{operationCode}/parameters", (string serialNo, string operationCode, RecordParameterRequest request, MesExecutionService service) =>
{
    service.RecordParameter(serialNo, operationCode, request.TagCode, request.Value, DateTimeOffset.UtcNow);
    return Results.Accepted();
});

app.MapPost("/api/serials/{serialNo}/operations/{operationCode}/complete", (string serialNo, string operationCode, MesExecutionService service) =>
{
    var task = service.CompleteOperation(serialNo, operationCode, DateTimeOffset.UtcNow);
    return Results.Accepted(value: new { inspectionTask = task });
});

app.MapPost("/api/serials/{serialNo}/operations/{operationCode}/inspection", (string serialNo, string operationCode, InspectionRequest request, MesExecutionService service) =>
{
    var nonconformance = service.RecordInspection(serialNo, operationCode, request.ItemCode, request.NumericValue, request.TextValue, request.Judgement, request.InspectorId, DateTimeOffset.UtcNow);
    return Results.Accepted(value: new { nonconformance });
});

app.MapPost("/api/nonconformances/{ncNo}/disposition", (string ncNo, DispositionRequest request, MesExecutionService executionService, MesIntegrationService integrationService) =>
{
    var nonconformance = executionService.DisposeNonconformance(ncNo, request.Disposition, request.ResponsibleDepartment, DateTimeOffset.UtcNow);
    integrationService.QueueQualityNotice(nonconformance.NcNo, DateTimeOffset.UtcNow);
    return Results.Ok(nonconformance);
});

app.MapPost("/api/equipment", (RegisterEquipmentRequest request, MesExecutionService service) =>
{
    var equipment = service.RegisterEquipment(request.EquipmentCode, request.Name, request.LineCode);
    return Results.Created($"/api/equipment/{equipment.EquipmentCode}", equipment);
});

app.MapPost("/api/equipment/{equipmentCode}/status", (string equipmentCode, EquipmentStatusRequest request, MesExecutionService service) =>
{
    service.UpdateEquipmentStatus(equipmentCode, request.Status);
    return Results.Accepted();
});

app.MapPost("/api/equipment/{equipmentCode}/samples", (string equipmentCode, EquipmentSampleRequest request, MesExecutionService service) =>
{
    service.RecordEquipmentSample(equipmentCode, request.TagCode, request.Value, request.Unit, request.OrderNo, request.SerialNo, request.OperationCode, DateTimeOffset.UtcNow);
    return Results.Accepted();
});

app.MapPost("/api/equipment/{equipmentCode}/alarms", (string equipmentCode, EquipmentAlarmRequest request, MesExecutionService executionService, MesIntegrationService integrationService) =>
{
    var alarm = executionService.RaiseEquipmentAlarm(equipmentCode, request.AlarmCode, request.Message, DateTimeOffset.UtcNow);
    integrationService.QueueEquipmentAlarmNotice(equipmentCode, alarm.AlarmNo, DateTimeOffset.UtcNow);
    return Results.Created($"/api/equipment/{equipmentCode}/alarms/{alarm.AlarmNo}", alarm);
});

app.MapGet("/api/trace/{serialNo}", (string serialNo, MesExecutionService service) => Results.Ok(service.Trace(serialNo)));
app.MapGet("/api/trace/material-lots/{materialLot}", (string materialLot, MesExecutionService service) => Results.Ok(service.TraceByMaterialLot(materialLot)));

app.Run();

static void SeedRoutes(IMesRepository repository)
{
    repository.AddRoute(new ProcessRoute("STATOR-A", "1.0", [
        new OperationDefinition("WINDING", "定子绕线", 10, ["CU-WIRE-001"], [new ParameterSpec("TENSION", 10m, 14m, "N", true), new ParameterSpec("TURNS", 48m, 48m, "turn", true)], false),
        new OperationDefinition("DIPPING", "浸漆固化", 20, ["VARNISH-001"], [new ParameterSpec("OVEN_TEMP", 130m, 150m, "℃", true)], false),
        new OperationDefinition("EOL_TEST", "定子终检", 30, [], [new ParameterSpec("IR", 100m, null, "MΩ", true)], true)
    ]));

    repository.AddRoute(new ProcessRoute("ROTOR-B", "1.0", [
        new OperationDefinition("PRESS", "转子压装", 10, ["SHAFT-001", "CORE-001"], [new ParameterSpec("PRESS_FORCE", 20m, 35m, "kN", true)], false),
        new OperationDefinition("MAGNET", "磁钢装配", 20, ["MAGNET-001", "GLUE-001"], [new ParameterSpec("GLUE_WEIGHT", 1.2m, 1.8m, "g", true)], false),
        new OperationDefinition("BALANCE", "动平衡", 30, [], [new ParameterSpec("UNBALANCE", 0m, 3m, "g.mm", true)], true)
    ]));
}

static void SeedEquipment(IMesRepository repository)
{
    repository.AddEquipment(new Equipment("EQ-WND-01", "绕线机 01", "L-STATOR-01"));
    repository.AddEquipment(new Equipment("EQ-DIP-01", "浸漆炉 01", "L-STATOR-01"));
    repository.AddEquipment(new Equipment("EQ-EOL-01", "定子终检台 01", "L-STATOR-01"));
    repository.AddEquipment(new Equipment("EQ-PRS-01", "压装机 01", "L-ROTOR-01"));
    repository.AddEquipment(new Equipment("EQ-MAG-01", "磁钢装配站 01", "L-ROTOR-01"));
    repository.AddEquipment(new Equipment("EQ-BAL-01", "动平衡机 01", "L-ROTOR-01"));
}

public sealed record ErpProductionPlanRequest(string PlanNo, string ProductCode, string RouteCode, string RouteVersion, int PlannedQuantity, string LineCode, DateOnly DueDate, int Priority);
public sealed record CreateOrderRequest(string OrderNo, string ProductCode, string RouteCode, string RouteVersion, int PlannedQuantity, string LineCode, DateOnly DueDate);
public sealed record ReleaseSerialRequest(string SerialNo);
public sealed record StartOperationRequest(string OperatorId, string WorkstationCode, string EquipmentCode);
public sealed record BindMaterialRequest(string MaterialCode, string LotNo, decimal Quantity);
public sealed record RecordParameterRequest(string TagCode, decimal Value);
public sealed record InspectionRequest(string ItemCode, decimal? NumericValue, string? TextValue, InspectionJudgement Judgement, string InspectorId);
public sealed record DispositionRequest(NonconformanceDisposition Disposition, string ResponsibleDepartment);
public sealed record RegisterEquipmentRequest(string EquipmentCode, string Name, string LineCode);
public sealed record EquipmentStatusRequest(EquipmentStatus Status);
public sealed record EquipmentSampleRequest(string TagCode, decimal Value, string Unit, string? OrderNo, string? SerialNo, string? OperationCode);
public sealed record EquipmentAlarmRequest(string AlarmCode, string Message);
