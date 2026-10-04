namespace ServerManager.Application.DTOs.Account;

/// <summary>İki adımlı doğrulamayı kapatma ve kurtarma kodlarını yenileme gibi hassas işlemlerde parola tekrar istenir.</summary>
public sealed class PasswordConfirmationDto
{
    public string Password { get; set; } = string.Empty;
}
