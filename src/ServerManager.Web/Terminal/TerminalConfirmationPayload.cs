namespace ServerManager.Web.Terminal;

public sealed record TerminalConfirmationPayload(string Token, string Command, string Description);
