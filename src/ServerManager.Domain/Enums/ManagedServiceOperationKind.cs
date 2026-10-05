namespace ServerManager.Domain.Enums;

public enum ManagedServiceOperationKind
{
    Install = 1,
    Recreate = 2,
    Upgrade = 3,
    Remove = 4
}
