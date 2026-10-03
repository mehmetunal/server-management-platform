using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.Validators.Docker;

namespace ServerManager.Application.Tests.Validators;

public class CreateNetworkDtoValidatorTests
{
    private readonly CreateNetworkDtoValidator _validator = new();

    [Fact]
    public void Accepts_minimal_network()
    {
        Assert.True(_validator.Validate(new CreateNetworkDto { Name = "appnet" }).IsValid);
    }

    [Fact]
    public void Accepts_network_with_subnet_and_gateway()
    {
        var dto = new CreateNetworkDto { Name = "appnet", Driver = "overlay", Subnet = "172.30.0.0/16", Gateway = "172.30.0.1", Internal = true };

        Assert.True(_validator.Validate(dto).IsValid);
    }

    [Theory]
    [InlineData("bridge")]
    [InlineData("host")]
    [InlineData("none")]
    public void Rejects_reserved_names(string name)
    {
        var result = _validator.Validate(new CreateNetworkDto { Name = name });

        Assert.Contains(result.Errors, e => e.ErrorMessage == "Bu ad Docker tarafından ayrılmıştır.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("app net")]
    [InlineData("-appnet")]
    public void Rejects_invalid_names(string name)
    {
        Assert.Contains(_validator.Validate(new CreateNetworkDto { Name = name }).Errors, e => e.PropertyName == nameof(CreateNetworkDto.Name));
    }

    [Fact]
    public void Rejects_unknown_driver()
    {
        Assert.Contains(_validator.Validate(new CreateNetworkDto { Name = "appnet", Driver = "host" }).Errors, e => e.PropertyName == nameof(CreateNetworkDto.Driver));
    }

    [Fact]
    public void Rejects_invalid_subnet_and_gateway()
    {
        var result = _validator.Validate(new CreateNetworkDto { Name = "appnet", Subnet = "172.30.0.0", Gateway = "x" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateNetworkDto.Subnet));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateNetworkDto.Gateway));
    }

    [Fact]
    public void Gateway_requires_subnet()
    {
        var result = _validator.Validate(new CreateNetworkDto { Name = "appnet", Gateway = "172.30.0.1" });

        Assert.Contains(result.Errors, e => e.ErrorMessage == "Gateway belirtildiğinde subnet de belirtilmelidir.");
    }
}
