namespace ServerManager.Application.Security;

public sealed record PublishedPort(string Container, string HostAddress, int HostPort, int ContainerPort, string Protocol);
