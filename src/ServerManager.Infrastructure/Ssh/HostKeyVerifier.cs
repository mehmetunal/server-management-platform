using System.Security.Cryptography;
using Renci.SshNet;

namespace ServerManager.Infrastructure.Ssh;

internal sealed class HostKeyVerifier
{
    public const string MismatchMessage =
        "Host key fingerprint kayıtlı değerle uyuşmuyor. Sunucu değişmiş veya bağlantı araya giren bir saldırıya maruz kalıyor olabilir.";

    private readonly string? _expectedFingerprint;

    public HostKeyVerifier(string? expectedFingerprint)
    {
        _expectedFingerprint = expectedFingerprint;
    }

    public string? ReceivedFingerprint { get; private set; }

    public bool Mismatch { get; private set; }

    public void Attach(SshClient client)
    {
        client.HostKeyReceived += (_, e) =>
        {
            ReceivedFingerprint = ComputeSha256Fingerprint(e.HostKey);
            if (_expectedFingerprint is not null
                && !string.Equals(_expectedFingerprint, ReceivedFingerprint, StringComparison.Ordinal))
            {
                Mismatch = true;
                e.CanTrust = false;
                return;
            }

            e.CanTrust = true;
        };
    }

    public static string ComputeSha256Fingerprint(byte[] hostKey)
    {
        var hash = SHA256.HashData(hostKey);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }
}
