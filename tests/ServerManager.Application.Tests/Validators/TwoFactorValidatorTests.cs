using ServerManager.Application.DTOs.Account;
using ServerManager.Application.Validators.Account;

namespace ServerManager.Application.Tests.Validators;

public class TwoFactorValidatorTests
{
    [Theory]
    [InlineData("123456", true)]
    [InlineData("123 456", true)]
    [InlineData("123-456", true)]
    [InlineData(" 123456 ", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12345a", false)]
    [InlineData("١٢٣٤٥٦", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Authenticator_code_must_be_six_ascii_digits(string? code, bool expected) =>
        Assert.Equal(expected, TwoFactorCodes.IsAuthenticatorCode(code));

    [Fact]
    public void Login_with_recovery_code_accepts_non_numeric_code()
    {
        var result = new TwoFactorLoginDtoValidator().Validate(new TwoFactorLoginDto { Code = "ab3cd-ef9gh", UseRecoveryCode = true });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Login_with_authenticator_rejects_recovery_style_code()
    {
        var result = new TwoFactorLoginDtoValidator().Validate(new TwoFactorLoginDto { Code = "ab3cd-ef9gh" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TwoFactorLoginDto.Code) && e.ErrorMessage == "Doğrulama kodu 6 haneli olmalıdır.");
    }

    [Fact]
    public void Empty_code_reports_single_required_message()
    {
        var result = new TwoFactorLoginDtoValidator().Validate(new TwoFactorLoginDto());

        var error = Assert.Single(result.Errors);
        Assert.Equal("Doğrulama kodunu girin.", error.ErrorMessage);
    }

    [Fact]
    public void Enable_requires_six_digit_code()
    {
        var validator = new EnableAuthenticatorDtoValidator();

        Assert.True(validator.Validate(new EnableAuthenticatorDto { Code = "654 321" }).IsValid);
        Assert.False(validator.Validate(new EnableAuthenticatorDto { Code = "65432" }).IsValid);
    }

    [Fact]
    public void Change_password_checks_length_confirmation_and_difference()
    {
        var validator = new ChangePasswordDtoValidator();

        Assert.True(validator.Validate(new ChangePasswordDto { CurrentPassword = "Old-Password1", NewPassword = "NewPassword12", ConfirmPassword = "NewPassword12" }).IsValid);

        var errors = validator.Validate(new ChangePasswordDto { CurrentPassword = "Same-Password1", NewPassword = "Same-Password1", ConfirmPassword = "Other" }).Errors;
        Assert.Contains(errors, e => e.PropertyName == nameof(ChangePasswordDto.NewPassword) && e.ErrorMessage == "Yeni parola mevcut paroladan farklı olmalıdır.");
        Assert.Contains(errors, e => e.PropertyName == nameof(ChangePasswordDto.ConfirmPassword));

        var shortErrors = validator.Validate(new ChangePasswordDto { CurrentPassword = "x", NewPassword = "Short1", ConfirmPassword = "Short1" }).Errors;
        Assert.Contains(shortErrors, e => e.PropertyName == nameof(ChangePasswordDto.NewPassword));
    }

    [Fact]
    public void Password_confirmation_requires_password()
    {
        var validator = new PasswordConfirmationDtoValidator();

        Assert.False(validator.Validate(new PasswordConfirmationDto()).IsValid);
        Assert.True(validator.Validate(new PasswordConfirmationDto { Password = "x" }).IsValid);
    }
}
