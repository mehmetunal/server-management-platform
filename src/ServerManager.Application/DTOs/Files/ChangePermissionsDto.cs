namespace ServerManager.Application.DTOs.Files;

public sealed class ChangePermissionsDto
{
    public string Path { get; set; } = string.Empty;

    /// <summary>Sekizli (0755) veya sembolik (u+x,g-w) mod; boşsa değiştirilmez.</summary>
    public string? Mode { get; set; }

    public string? Owner { get; set; }

    public string? Group { get; set; }

    public bool Recursive { get; set; }
}
