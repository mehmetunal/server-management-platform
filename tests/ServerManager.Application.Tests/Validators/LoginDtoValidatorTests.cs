using ServerManager.Application.DTOs.Account;
using ServerManager.Application.Validators.Account;

namespace ServerManager.Application.Tests.Validators;

public class LoginDtoValidatorTests
{
    private readonly LoginDtoValidator _validator = new();

    [Fact]
    public void Valid_login_passes()
    {
        var result = _validator.Validate(new LoginDto { Email = "admin@example.com", Password = "x" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Empty_fields_fail_with_turkish_messages()
    {
        var result = _validator.Validate(new LoginDto());

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginDto.Email) && e.ErrorMessage == "E-posta zorunludur.");
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginDto.Password) && e.ErrorMessage == "Parola zorunludur.");
    }
}
