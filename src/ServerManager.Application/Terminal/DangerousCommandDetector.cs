using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServerManager.Application.Terminal;

public sealed class DangerousCommandDetector
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly DangerousCommandMode _mode;
    private readonly IReadOnlyList<(Regex Regex, string Description)> _rules;

    public DangerousCommandDetector(IOptions<TerminalOptions> options, ILogger<DangerousCommandDetector> logger)
    {
        var value = options.Value;
        _mode = value.DangerousCommandMode;

        var rules = value.DangerousCommands.Count > 0 ? value.DangerousCommands : DangerousCommandRule.Defaults;
        var compiled = new List<(Regex, string)>();
        foreach (var rule in rules.Where(r => !string.IsNullOrWhiteSpace(r.Pattern)))
        {
            try
            {
                compiled.Add((
                    new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout),
                    string.IsNullOrWhiteSpace(rule.Description) ? rule.Pattern : rule.Description));
            }
            catch (ArgumentException)
            {
                logger.LogWarning("Geçersiz tehlikeli komut deseni yok sayıldı: {Pattern}", rule.Pattern);
            }
        }

        _rules = compiled;
    }

    public DangerousCommandMode Mode => _mode;

    public DangerousCommandMatch? Detect(string commandLine)
    {
        if (_mode == DangerousCommandMode.Off || string.IsNullOrWhiteSpace(commandLine))
            return null;

        foreach (var line in commandLine.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var (regex, description) in _rules)
            {
                try
                {
                    if (regex.IsMatch(line))
                        return new DangerousCommandMatch(description, _mode);
                }
                catch (RegexMatchTimeoutException)
                {
                    return new DangerousCommandMatch(description, _mode);
                }
            }
        }

        return null;
    }
}
