namespace ServerManager.Domain.Enums;

/// <summary>Servisin panel tarafındaki durumu; son işlemin sonucunu yansıtır. Container'ın anlık durumu sunucudan okunur.</summary>
public enum ManagedServiceStatus
{
    Installing = 0,
    Running = 1,
    Failed = 2,
    Updating = 3,
    Removing = 4,
    Removed = 5
}
