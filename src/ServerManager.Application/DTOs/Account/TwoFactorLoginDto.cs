namespace ServerManager.Application.DTOs.Account;

public sealed class TwoFactorLoginDto
{
    public string Code { get; set; } = string.Empty;

    public bool UseRecoveryCode { get; set; }

    /// <summary>Bu tarayıcıda 30 gün boyunca kod sorulmaz.</summary>
    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
