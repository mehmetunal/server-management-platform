using ServerManager.Application.DTOs.Users;
using ServerManager.Application.Validators.Users;

namespace ServerManager.Application.Tests.Validators;

public class CreateUserDtoValidatorTests
{
    private readonly CreateUserDtoValidator _validator = new();

    private static CreateUserDto ValidDto() => new()
    {
        Email = "operator@example.com",
        FullName = "Test Operator",
        Password = "Str0ngPassword",
        ConfirmPassword = "Str0ngPassword",
        Roles = [ServerManager.Application.Authorization.Roles.Operator]
    };

    [Fact]
    public void Valid_user_passes()
    {
        Assert.True(_validator.Validate(ValidDto()).IsValid);
    }

    [Fact]
    public void Short_password_fails()
    {
        var dto = ValidDto();
        dto.Password = dto.ConfirmPassword = "Ab1";

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserDto.Password));
    }

    [Fact]
    public void Mismatched_confirmation_fails()
    {
        var dto = ValidDto();
        dto.ConfirmPassword = "Different1234";

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserDto.ConfirmPassword));
    }

    [Fact]
    public void Missing_role_fails()
    {
        var dto = ValidDto();
        dto.Roles = [];

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserDto.Roles));
    }

    [Fact]
    public void Invalid_email_fails()
    {
        var dto = ValidDto();
        dto.Email = "not-an-email";

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserDto.Email));
    }
}
