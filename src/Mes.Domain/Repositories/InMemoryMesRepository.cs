using Mes.Domain.Entities;

namespace Mes.Domain.Repositories;

public sealed class InMemoryMesRepository : IMesRepository
{
    private readonly Dictionary<string, ProductionOrder> _orders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SerialUnit> _serialUnits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProcessRoute> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InspectionTask> _inspectionTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Nonconformance> _nonconformances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Equipment> _equipment = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IntegrationMessage> _integrationMessages = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ProductionOrder> Orders => _orders.Values;
    public IReadOnlyCollection<SerialUnit> SerialUnits => _serialUnits.Values;
    public IReadOnlyCollection<ProcessRoute> Routes => _routes.Values;
    public IReadOnlyCollection<InspectionTask> InspectionTasks => _inspectionTasks.Values;
    public IReadOnlyCollection<Nonconformance> Nonconformances => _nonconformances.Values;
    public IReadOnlyCollection<Equipment> Equipment => _equipment.Values;
    public IReadOnlyCollection<IntegrationMessage> IntegrationMessages => _integrationMessages.Values;

    public void AddRoute(ProcessRoute route)
    {
        _routes[RouteKey(route.Code, route.Version)] = route;
    }

    public void AddOrder(ProductionOrder order)
    {
        _orders.Add(order.OrderNo, order);
    }

    public void AddSerialUnit(SerialUnit unit)
    {
        _serialUnits.Add(unit.SerialNo, unit);
    }

    public void AddInspectionTask(InspectionTask task)
    {
        _inspectionTasks.Add(task.TaskNo, task);
    }

    public void UpdateInspectionTask(InspectionTask task)
    {
        _inspectionTasks[task.TaskNo] = task;
    }

    public void AddNonconformance(Nonconformance nonconformance)
    {
        _nonconformances.Add(nonconformance.NcNo, nonconformance);
    }

    public void UpdateNonconformance(Nonconformance nonconformance)
    {
        _nonconformances[nonconformance.NcNo] = nonconformance;
    }

    public void AddEquipment(Equipment equipment)
    {
        _equipment[equipment.EquipmentCode] = equipment;
    }

    public void AddIntegrationMessage(IntegrationMessage message)
    {
        _integrationMessages.Add(message.MessageId, message);
    }

    public void UpdateIntegrationMessage(IntegrationMessage message)
    {
        _integrationMessages[message.MessageId] = message;
    }

    public ProductionOrder GetOrder(string orderNo)
    {
        return _orders.TryGetValue(orderNo, out var order)
            ? order
            : throw new KeyNotFoundException($"Production order {orderNo} does not exist.");
    }

    public ProcessRoute GetRoute(string routeCode, string routeVersion)
    {
        return _routes.TryGetValue(RouteKey(routeCode, routeVersion), out var route)
            ? route
            : throw new KeyNotFoundException($"Route {routeCode}/{routeVersion} does not exist.");
    }

    public SerialUnit GetSerialUnit(string serialNo)
    {
        return _serialUnits.TryGetValue(serialNo, out var unit)
            ? unit
            : throw new KeyNotFoundException($"Serial unit {serialNo} does not exist.");
    }

    public InspectionTask GetInspectionTask(string taskNo)
    {
        return _inspectionTasks.TryGetValue(taskNo, out var task)
            ? task
            : throw new KeyNotFoundException($"Inspection task {taskNo} does not exist.");
    }

    public Nonconformance GetNonconformance(string ncNo)
    {
        return _nonconformances.TryGetValue(ncNo, out var nonconformance)
            ? nonconformance
            : throw new KeyNotFoundException($"Nonconformance {ncNo} does not exist.");
    }

    public Equipment GetEquipment(string equipmentCode)
    {
        return _equipment.TryGetValue(equipmentCode, out var equipment)
            ? equipment
            : throw new KeyNotFoundException($"Equipment {equipmentCode} does not exist.");
    }

    public IntegrationMessage GetIntegrationMessage(string messageId)
    {
        return _integrationMessages.TryGetValue(messageId, out var message)
            ? message
            : throw new KeyNotFoundException($"Integration message {messageId} does not exist.");
    }

    private static string RouteKey(string routeCode, string routeVersion)
    {
        return $"{routeCode}:{routeVersion}";
    }
}
