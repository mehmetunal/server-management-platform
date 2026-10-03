namespace ServerManager.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? UserName { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    bool IsAuthenticated { get; }
}
