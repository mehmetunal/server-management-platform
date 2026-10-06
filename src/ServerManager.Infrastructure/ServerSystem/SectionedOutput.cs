namespace ServerManager.Infrastructure.ServerSystem;

/// <summary>"@@ss:ad" satırlarıyla bölümlere ayrılmış betik çıktısı (ServerSystem, Temizlik ve Kaynak Kullanımı betikleri).</summary>
internal sealed class SectionedOutput
{
    private readonly Dictionary<string, List<string>> _sections;

    private SectionedOutput(Dictionary<string, List<string>> sections) => _sections = sections;

    public static SectionedOutput Parse(string output)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var raw in output.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith(ServerSystemCommands.SectionPrefix, StringComparison.Ordinal))
            {
                current = [];
                sections[line[ServerSystemCommands.SectionPrefix.Length..]] = current;
                continue;
            }

            current?.Add(line);
        }

        return new SectionedOutput(sections);
    }

    public bool Has(string name) => _sections.ContainsKey(name);

    public IReadOnlyList<string> this[string name] => _sections.TryGetValue(name, out var lines) ? lines : [];

    /// <summary>"anahtar=değer" satırındaki değer (ör. tool=vmstat).</summary>
    public string? Value(string section, string key) =>
        this[section].Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith(key + "=", StringComparison.Ordinal))?[(key.Length + 1)..];

    public IEnumerable<string> Lines(string section) =>
        this[section].Where(l => l.Trim().Length > 0);
}
