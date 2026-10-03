using System.Text.RegularExpressions;

namespace ServerManager.Application.Terminal;

/// <summary>
/// Terminal çıktısından iki durumu izler: tam ekran uygulama (vim, less, top) açık mı ve
/// son satır bir parola sorusu mu. İkisinde de kullanıcının yazdıkları komut olarak kaydedilmez.
/// </summary>
public sealed partial class TerminalOutputMonitor
{
    private const int TailLength = 256;
    private const int CarryLength = 12;

    private static readonly string[] AlternateScreenOn = ["\u001b[?1049h", "\u001b[?1047h", "\u001b[?47h"];
    private static readonly string[] AlternateScreenOff = ["\u001b[?1049l", "\u001b[?1047l", "\u001b[?47l"];

    private readonly Lock _gate = new();
    private string _carry = string.Empty;
    private string _plainTail = string.Empty;
    private bool _alternateScreen;

    public bool IsAlternateScreen
    {
        get
        {
            lock (_gate)
                return _alternateScreen;
        }
    }

    public bool IsSecretPrompt
    {
        get
        {
            lock (_gate)
            {
                var lastLineStart = _plainTail.LastIndexOf('\n') + 1;
                return SecretPromptPattern().IsMatch(_plainTail.AsSpan(lastLineStart));
            }
        }
    }

    public void Inspect(string output)
    {
        if (string.IsNullOrEmpty(output))
            return;

        lock (_gate)
        {
            var combined = _carry + output;
            var lastOn = LastIndexOfAny(combined, AlternateScreenOn);
            var lastOff = LastIndexOfAny(combined, AlternateScreenOff);
            if (lastOn >= 0 || lastOff >= 0)
                _alternateScreen = lastOn > lastOff;

            _carry = combined.Length <= CarryLength ? combined : combined[^CarryLength..];

            var plain = _plainTail + AnsiSequencePattern().Replace(output, string.Empty).Replace("\r", string.Empty);
            _plainTail = plain.Length <= TailLength ? plain : plain[^TailLength..];
        }
    }

    private static int LastIndexOfAny(string text, string[] values)
    {
        var last = -1;
        foreach (var value in values)
            last = Math.Max(last, text.LastIndexOf(value, StringComparison.Ordinal));
        return last;
    }

    [GeneratedRegex(@"(password|passphrase|passcode|parola|şifre|sifre)[^\n]*[:：]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretPromptPattern();

    [GeneratedRegex(@"\u001b(\[[0-?]*[ -/]*[@-~]|\][^\u0007\u001b]*(\u0007|\u001b\\)?|[@-Z\\-_])")]
    private static partial Regex AnsiSequencePattern();
}
