namespace ServerManager.Application.DTOs.Servers;

public sealed class UpdateServerDto : ServerFormDto
{
    public Guid Id { get; set; }

    public bool HasPassword { get; set; }

    public bool HasPrivateKey { get; set; }

    public bool HasPassphrase { get; set; }

    public bool HasSudoPassword { get; set; }
}
