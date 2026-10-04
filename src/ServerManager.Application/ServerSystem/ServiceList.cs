namespace ServerManager.Application.ServerSystem;

public sealed record ServiceList(ServiceManagerKind Manager, IReadOnlyList<ServiceUnit> Units);
