using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ServerManager.Application.Interfaces.Security;

namespace ServerManager.Infrastructure.Security;

public sealed class HmacAuditChainSigner : IAuditChainSigner
{
    private static readonly byte[] KeyInfo = Encoding.UTF8.GetBytes("ServerManager.AuditChain.v1");

    private readonly byte[] _key;

    public HmacAuditChainSigner(IOptions<SecurityOptions> options)
    {
        var validation = new SecurityOptionsValidator().Validate(null, options.Value);
        if (validation.Failed)
            throw new InvalidOperationException(validation.FailureMessage);

        var masterKey = Convert.FromBase64String(options.Value.MasterKey);
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, info: KeyInfo);
        CryptographicOperations.ZeroMemory(masterKey);
    }

    public string Sign(string canonicalPayload) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(canonicalPayload)));
}
