using ServerManager.Application.Authorization;

namespace ServerManager.Application.DTOs.Users;

public sealed class CreateUserDto
{
    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.Viewer;
}
