using ServerManager.Plugin.DevOps.Dokploy.DTOs;
using ServerManager.Plugin.DevOps.Dokploy.Validators;

namespace ServerManager.Plugin.DevOps.Dokploy.Tests.Validators;

public class DokployValidatorsTests
{
    private readonly DokploySettingsDtoValidator _settings = new();
    private readonly DokployInstallRequestDtoValidator _install = new();

    [Theory]
    [InlineData("http://203.0.113.10:3000", null)]
    [InlineData("https://panel.example.com", "abcDEF123_-.")]
    public void Accepts_valid_settings(string baseUrl, string? apiKey)
    {
        Assert.True(_settings.Validate(new DokploySettingsDto { BaseUrl = baseUrl, ApiKey = apiKey }).IsValid);
    }

    [Theory]
    [InlineData("", null, "BaseUrl")]
    [InlineData("javascript:alert(1)", null, "BaseUrl")]
    [InlineData("http://203.0.113.10:3000", "has space", "ApiKey")]
    public void Rejects_invalid_settings(string baseUrl, string? apiKey, string property)
    {
        var result = _settings.Validate(new DokploySettingsDto { BaseUrl = baseUrl, ApiKey = apiKey });

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Rejects_too_long_api_key()
    {
        var result = _settings.Validate(new DokploySettingsDto { BaseUrl = "http://h:3000", ApiKey = new string('k', 513) });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DokploySettingsDto.ApiKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("canary")]
    [InlineData("v0.25.3")]
    public void Install_request_accepts_empty_or_valid_version(string? version)
    {
        Assert.True(_install.Validate(new DokployInstallRequestDto { Version = version, ConfirmationName = "web-01" }).IsValid);
    }

    [Fact]
    public void Install_request_rejects_bad_version_and_missing_confirmation()
    {
        var result = _install.Validate(new DokployInstallRequestDto { Version = "latest && reboot" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DokployInstallRequestDto.Version));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DokployInstallRequestDto.ConfirmationName));
    }
}
