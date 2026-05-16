using Mes.Domain.Entities;

namespace Mes.Domain.Repositories;

public interface IMesRepository
{
    IReadOnlyCollection<ProductionOrder> Orders { get; }
    IReadOnlyCollection<SerialUnit> SerialUnits { get; }
    IReadOnlyCollection<ProcessRoute> Routes { get; }
    IReadOnlyCollection<InspectionTask> InspectionTasks { get; }
    IReadOnlyCollection<Nonconformance> Nonconformances { get; }
    IReadOnlyCollection<Equipment> Equipment { get; }
    IReadOnlyCollection<IntegrationMessage> IntegrationMessages { get; }
    void AddRoute(ProcessRoute route);
    void AddOrder(ProductionOrder order);
    void AddSerialUnit(SerialUnit unit);
    void AddInspectionTask(InspectionTask task);
    void UpdateInspectionTask(InspectionTask task);
    void AddNonconformance(Nonconformance nonconformance);
    void UpdateNonconformance(Nonconformance nonconformance);
    void AddEquipment(Equipment equipment);
    void AddIntegrationMessage(IntegrationMessage message);
    void UpdateIntegrationMessage(IntegrationMessage message);
    ProductionOrder GetOrder(string orderNo);
    ProcessRoute GetRoute(string routeCode, string routeVersion);
    SerialUnit GetSerialUnit(string serialNo);
    InspectionTask GetInspectionTask(string taskNo);
    Nonconformance GetNonconformance(string ncNo);
    Equipment GetEquipment(string equipmentCode);
    IntegrationMessage GetIntegrationMessage(string messageId);
}
