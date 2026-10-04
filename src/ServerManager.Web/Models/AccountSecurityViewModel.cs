using ServerManager.Application.DTOs.Account;

namespace ServerManager.Web.Models;

public sealed class AccountSecurityViewModel
{
    public required AccountSecurityDto Security { get; init; }

    public bool TwoFactorRequired { get; init; }

    public ChangePasswordDto ChangePassword { get; init; } = new();
}
