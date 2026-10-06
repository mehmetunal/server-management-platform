namespace ServerManager.Application.DTOs.Users;

public sealed class UpdateUserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    /// <summary>Kullanıcının rolleri (en az bir). İzinler rollerin birleşimidir.</summary>
    public List<string> Roles { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public string? NewPassword { get; set; }

    public string? ConfirmNewPassword { get; set; }
}
