using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Tests.TestData;
using ServerManager.Application.Validators.Servers;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Validators;

public class CreateServerDtoValidatorTests
{
    private readonly CreateServerDtoValidator _validator = new();

    [Fact]
    public void Valid_password_server_passes()
    {
        var result = _validator.Validate(ServerTestData.ValidCreateDto());

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("999.1.1.1")]
    [InlineData("not-an-ip")]
    public void Invalid_ip_address_fails(string ip)
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.IpAddress = ip;

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.IpAddress));
    }

    [Fact]
    public void Ipv6_address_passes()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.IpAddress = "2001:db8::10";

        Assert.True(_validator.Validate(dto).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-1)]
    public void Port_out_of_range_fails(int port)
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.SshPort = port;

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.SshPort));
    }

    [Theory]
    [InlineData("-bad-host")]
    [InlineData("host_name")]
    [InlineData("host..example.com")]
    public void Invalid_hostname_fails(string hostname)
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.Hostname = hostname;

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.Hostname));
    }

    [Theory]
    [InlineData("root;rm -rf")]
    [InlineData("1user")]
    [InlineData("")]
    public void Invalid_username_fails(string username)
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.Username = username;

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.Username));
    }

    [Fact]
    public void Password_auth_without_password_fails()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.Password = null;

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.Password));
    }

    [Fact]
    public void Private_key_auth_requires_valid_key()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.AuthenticationType = AuthenticationType.PrivateKey;
        dto.Password = null;
        dto.PrivateKey = "not a key";

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.PrivateKey));
    }

    [Fact]
    public void Private_key_with_passphrase_requires_passphrase()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.AuthenticationType = AuthenticationType.PrivateKeyWithPassphrase;
        dto.Password = null;
        dto.PrivateKey = ServerTestData.SamplePrivateKey;

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.Passphrase));
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(CreateServerDto.PrivateKey));
    }

    [Fact]
    public void Invalid_tag_fails()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.Tags = "prod, web server!";

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.Tags));
    }

    [Fact]
    public void Undefined_enum_value_fails()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.Environment = (ServerEnvironment)42;

        var result = _validator.Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.Environment));
    }
}
