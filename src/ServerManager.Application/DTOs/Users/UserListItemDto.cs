namespace ServerManager.Application.DTOs.Users;

public sealed class UserListItemDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? FullName { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool IsActive { get; init; }

    public bool IsLockedOut { get; init; }

    public bool TwoFactorEnabled { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? LastLoginAt { get; init; }
}
