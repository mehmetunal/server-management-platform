namespace ServerManager.Web.Terminal;

public sealed record TerminalAttachResponse(
    bool Success,
    string? Message,
    string? Output = null,
    string? Title = null,
    TerminalConfirmationPayload? PendingConfirmation = null);
