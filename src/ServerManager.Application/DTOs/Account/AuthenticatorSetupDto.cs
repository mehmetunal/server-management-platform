namespace ServerManager.Application.DTOs.Account;

public sealed class AuthenticatorSetupDto
{
    /// <summary>Elle giriş için 4'erli gruplanmış anahtar.</summary>
    public string SharedKey { get; init; } = string.Empty;

    /// <summary>otpauth:// adresi; QR kodu bundan üretilir.</summary>
    public string AuthenticatorUri { get; init; } = string.Empty;
}
