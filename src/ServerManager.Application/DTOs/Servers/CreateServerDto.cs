namespace ServerManager.Application.DTOs.Servers;

public sealed class CreateServerDto : ServerFormDto
{
    /// <summary>Bulut hesabından içe aktarılırken dolu gelir; sunucu bu hesaba bağlanır.</summary>
    public Guid? CloudAccountId { get; set; }

    public string? CloudExternalId { get; set; }
}
