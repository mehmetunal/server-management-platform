using System.Security.Cryptography;
using System.Text;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Auditing;

public static class AuditChainVerifier
{
    /// <summary>
    /// Kayıtlar Id sırasıyla gelmelidir. İmzasız kayıtlar yalnızca zincirin başında (özellik öncesi) kabul edilir;
    /// imzalı kayıtlardan sonra gelen imzasız kayıt, silinmiş imza olarak değerlendirilir.
    /// </summary>
    public static async Task<AuditChainVerificationDto> VerifyAsync(
        IAsyncEnumerable<AuditLog> logs, IAuditChainSigner signer, CancellationToken cancellationToken = default)
    {
        string? previousHash = null;
        var checkedCount = 0;
        var signedCount = 0;
        var unsignedCount = 0;

        await foreach (var log in logs.WithCancellation(cancellationToken))
        {
            checkedCount++;

            if (log.ChainHash is null)
            {
                if (signedCount > 0)
                    return Broken(log, checkedCount, signedCount, unsignedCount, "İmzalı kayıtlardan sonra imzasız kayıt var (imza silinmiş olabilir).");

                unsignedCount++;
                continue;
            }

            var expected = signer.Sign(AuditChainFormat.Canonicalize(previousHash, log));
            if (!FixedTimeEquals(expected, log.ChainHash))
                return Broken(log, checkedCount, signedCount, unsignedCount, "Kayıt değiştirilmiş, silinmiş ya da araya kayıt eklenmiş.");

            signedCount++;
            previousHash = log.ChainHash;
        }

        return new AuditChainVerificationDto
        {
            IsValid = true,
            CheckedCount = checkedCount,
            SignedCount = signedCount,
            UnsignedCount = unsignedCount,
            Message = signedCount == 0
                ? "Henüz imzalı kayıt yok."
                : $"{signedCount} imzalı kaydın zinciri sağlam."
        };
    }

    private static bool FixedTimeEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));

    private static AuditChainVerificationDto Broken(AuditLog log, int checkedCount, int signedCount, int unsignedCount, string reason) => new()
    {
        IsValid = false,
        CheckedCount = checkedCount,
        SignedCount = signedCount,
        UnsignedCount = unsignedCount,
        BrokenAtId = log.Id,
        BrokenAtTime = log.CreatedAt,
        Message = $"Zincir #{log.Id} numaralı kayıtta kırılıyor. {reason}"
    };
}
