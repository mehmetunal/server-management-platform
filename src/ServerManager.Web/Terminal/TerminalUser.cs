using System.Security.Claims;

namespace ServerManager.Web.Terminal;

public sealed record TerminalUser(string ConnectionId, string UserId, string? UserName, string? IpAddress, ClaimsPrincipal Principal);
