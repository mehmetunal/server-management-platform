using ServerManager.Application.Authorization;

namespace ServerManager.Application.DTOs.Users;

public sealed class UpdateUserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string Role { get; set; } = Roles.Viewer;

    public bool IsActive { get; set; } = true;

    public string? NewPassword { get; set; }

    public string? ConfirmNewPassword { get; set; }
}
