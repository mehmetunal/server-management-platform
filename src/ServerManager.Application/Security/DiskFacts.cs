namespace ServerManager.Application.Security;

public sealed class DiskFacts
{
    /// <summary>lsblk çalıştıysa true.</summary>
    public bool Checked { get; init; }

    /// <summary>crypt türündeki veya crypto_LUKS dosya sistemli aygıtlar.</summary>
    public IReadOnlyList<string> EncryptedDevices { get; init; } = [];
}
