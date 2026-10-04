namespace ServerManager.Application.Interfaces.Security;

public interface IAuditChainSigner
{
    /// <summary>Uygulama anahtarından türetilen anahtarla HMAC-SHA256 (küçük harf hex).</summary>
    string Sign(string canonicalPayload);
}
