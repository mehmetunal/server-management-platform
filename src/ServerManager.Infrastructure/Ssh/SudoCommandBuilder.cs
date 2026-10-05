using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Infrastructure.Ssh;

internal static class SudoCommandBuilder
{
    /// <summary>sudo'nun bastığı prompt. Komut satırında %% olarak yazılır; böylece PTY'nin geri yansıttığı komut metni bu işaretle eşleşmez.</summary>
    public const string TerminalPromptMarker = "[sm-sudo%prompt]";

    private const string TerminalPromptArgument = "[sm-sudo%%prompt]";

    /// <summary>
    /// Parola varsa stdin'in ilk satırıdır ve komut satırına hiçbir zaman yazılmaz. Sarmalayıcı bu satırı her durumda kendisi okur;
    /// böylece sudo parola sormadığında (NOPASSWD, önbellekteki oturum) parola komutun stdin'ine (override dosyası, geri yükleme
    /// arşivi …) karışmaz. Parola yalnızca sudo'ya verilir: önce <c>sudo -v</c> ile doğrulanır, sudo oturumu önbelleğe alıyorsa
    /// komut <c>sudo -n</c> ile, almıyorsa (timestamp_timeout=0) parola yeniden sudo'ya verilerek çalışır.
    /// Parola yoksa -n ile parolasız sudo denenir ve gerekirse hemen hata verir.
    /// </summary>
    public static string Build(RemoteExecutionContext context, string commandText, bool elevate)
    {
        if (!elevate || !context.UseSudo)
            return commandText;

        if (string.IsNullOrEmpty(context.SudoPassword))
            return "sudo -n -- " + commandText;

        var script =
            "IFS= read -r sm_sudo_pw || sm_sudo_pw=\n" +
            "if ! sudo -n true 2>/dev/null; then\n" +
            "  printf '%s\\n' \"$sm_sudo_pw\" | sudo -S -p '' -v || exit 1\n" +
            "fi\n" +
            "if sudo -n true 2>/dev/null; then\n" +
            "  unset sm_sudo_pw\n" +
            "  sudo -n -- " + commandText + "\n" +
            "else\n" +
            "  { printf '%s\\n' \"$sm_sudo_pw\"; unset sm_sudo_pw; cat; } | sudo -S -p '' -- " + commandText + "\n" +
            "fi\n";
        return "sh -c " + ShellQuote.Quote(script);
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
