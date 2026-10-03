namespace ServerManager.Application.Common;

public enum ServiceErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    Failure = 5
}
