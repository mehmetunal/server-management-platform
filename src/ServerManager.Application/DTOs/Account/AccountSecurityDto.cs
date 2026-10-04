namespace ServerManager.Application.DTOs.Account;

public sealed class AccountSecurityDto
{
    public string Email { get; init; } = string.Empty;

    public string? FullName { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool TwoFactorEnabled { get; init; }

    public int RecoveryCodesLeft { get; init; }

    public bool IsMachineRemembered { get; init; }

    public DateTime? LastLoginAt { get; init; }
}
