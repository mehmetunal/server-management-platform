namespace ServerManager.Web.Terminal;

public sealed record TerminalStartResponse(bool Success, string? Message, Guid? SessionId = null);
