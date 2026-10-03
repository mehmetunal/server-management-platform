using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Infrastructure.Ssh;

internal static class SudoCommandBuilder
{
    /// <summary>sudo'nun bastığı prompt. Komut satırında %% olarak yazılır; böylece PTY'nin geri yansıttığı komut metni bu işaretle eşleşmez.</summary>
    public const string TerminalPromptMarker = "[sm-sudo%prompt]";

    private const string TerminalPromptArgument = "[sm-sudo%%prompt]";

    /// <summary>
    /// Parola varsa sudo stdin'den okur (-S, boş prompt); parola komut satırına hiçbir zaman yazılmaz.
    /// Parola yoksa -n ile parolasız sudo denenir ve gerekirse hemen hata verir.
    /// </summary>
    public static string Build(RemoteExecutionContext context, string commandText, bool elevate)
    {
        if (!elevate || !context.UseSudo)
            return commandText;

        return string.IsNullOrEmpty(context.SudoPassword)
            ? "sudo -n -- " + commandText
            : "sudo -S -p '' -- " + commandText;
    }

    public static bool RequiresPasswordInput(RemoteExecutionContext context, bool elevate) =>
        elevate && context.UseSudo && !string.IsNullOrEmpty(context.SudoPassword);

    /// <summary>Etkileşimli terminal için: sudo prompt'u bilinen bir işaretle basılır ve parola PTY'ye otomatik yazılır.</summary>
    public static string BuildInteractive(RemoteExecutionContext context, string commandText, bool elevate)
    {
        if (!elevate || !context.UseSudo)
            return commandText;

        return string.IsNullOrEmpty(context.SudoPassword)
            ? "sudo -n -- " + commandText
            : $"sudo -p '{TerminalPromptArgument}' -- " + commandText;
    }
}
