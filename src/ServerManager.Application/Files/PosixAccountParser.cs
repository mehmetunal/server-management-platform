namespace ServerManager.Application.Files;

/// <summary>/etc/passwd ve /etc/group satırlarından sayısal kimlik → ad eşlemesi çıkarır (ad:x:id:...).</summary>
public static class PosixAccountParser
{
    public static IReadOnlyDictionary<int, string> Parse(string? content)
    {
        var names = new Dictionary<int, string>();
        if (string.IsNullOrEmpty(content))
            return names;

        foreach (var line in content.Split('\n'))
        {
            if (line.Length == 0 || line[0] == '#')
                continue;

            var fields = line.Split(':');
            if (fields.Length >= 3 && fields[0].Length > 0 && int.TryParse(fields[2], out var id))
                names.TryAdd(id, fields[0]);
        }

        return names;
    }
}
