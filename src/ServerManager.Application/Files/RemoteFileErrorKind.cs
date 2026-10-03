namespace ServerManager.Application.Files;

public enum RemoteFileErrorKind
{
    Failure = 0,
    NotFound = 1,
    PermissionDenied = 2,
    AlreadyExists = 3
}
