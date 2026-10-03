namespace ServerManager.Web.Terminal;

/// <param name="Level">info, warning veya error.</param>
public sealed record TerminalNoticePayload(string Level, string Message);
