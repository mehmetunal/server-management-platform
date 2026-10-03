using ServerManager.Application.DTOs.Servers;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.TestData;

public static class ServerTestData
{
    public const string SamplePrivateKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAAtest\n-----END OPENSSH PRIVATE KEY-----";

    public static CreateServerDto ValidCreateDto() => new()
    {
        Name = "Production-01",
        Hostname = "web-01.example.com",
        IpAddress = "203.0.113.10",
        SshPort = 22,
        Username = "deploy",
        AuthenticationType = AuthenticationType.Password,
        Password = "S3cret-pass!",
        Environment = ServerEnvironment.Production,
        Tags = "production, web"
    };

    public static UpdateServerDto ValidUpdateDto(Guid id) => new()
    {
        Id = id,
        Name = "Production-01",
        Hostname = "web-01.example.com",
        IpAddress = "203.0.113.10",
        SshPort = 22,
        Username = "deploy",
        AuthenticationType = AuthenticationType.Password,
        Environment = ServerEnvironment.Production,
        Tags = "production, web"
    };
}
