namespace ServerManager.Web.Models;

public sealed class AuthenticatorSetupResponse
{
    public string SharedKey { get; init; } = string.Empty;

    public string QrCodeDataUri { get; init; } = string.Empty;
}
