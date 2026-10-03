using ServerManager.Domain.Common;

namespace ServerManager.Domain.Entities;

public class ServerCredential : BaseEntity
{
    public Guid ServerId { get; set; }

    public string? EncryptedPassword { get; set; }

    public string? EncryptedPrivateKey { get; set; }

    public string? EncryptedPassphrase { get; set; }

    public string? EncryptedSudoPassword { get; set; }

    public int KeyVersion { get; set; } = 1;

    public Server? Server { get; set; }
}
