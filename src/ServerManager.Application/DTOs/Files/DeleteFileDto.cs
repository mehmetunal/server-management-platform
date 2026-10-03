namespace ServerManager.Application.DTOs.Files;

public sealed class DeleteFileDto
{
    public string Path { get; set; } = string.Empty;

    /// <summary>Klasör silinirken onay olarak klasör adı yazılmalıdır.</summary>
    public string? ConfirmationName { get; set; }
}
