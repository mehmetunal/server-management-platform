using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.Validators.Docker;

namespace ServerManager.Application.Tests.Validators;

public class PullImageDtoValidatorTests
{
    private readonly PullImageDtoValidator _validator = new();

    [Theory]
    [InlineData("nginx:alpine")]
    [InlineData("ghcr.io/org/app:1.2.0")]
    public void Accepts_valid_references(string reference)
    {
        Assert.True(_validator.Validate(new PullImageDto { Reference = reference }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nginx alpine")]
    [InlineData("nginx;reboot")]
    [InlineData("--all-tags")]
    public void Rejects_invalid_references(string reference)
    {
        Assert.False(_validator.Validate(new PullImageDto { Reference = reference }).IsValid);
    }
}
