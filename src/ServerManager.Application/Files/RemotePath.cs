namespace ServerManager.Application.Files;

/// <summary>Uzak sunucudaki POSIX yolları için doğrulama ve birleştirme.</summary>
public static class RemotePath
{
    public const int MaxPathLength = 4096;
    public const int MaxNameLength = 255;

    /// <summary>Mutlak yolu sadeleştirir ('.', '..', tekrar eden '/'). Geçersizse null döner.</summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength || !path.StartsWith('/') || HasControlCharacter(path))
            return null;

        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;

            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return "/" + string.Join('/', segments);
    }

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= MaxNameLength
        && name != "."
        && name != ".."
        && !name.Contains('/')
        && !HasControlCharacter(name);

    public static string Combine(string directory, string name) =>
        directory.EndsWith('/') ? directory + name : directory + "/" + name;

    public static string? GetParent(string path)
    {
        if (path == "/")
            return null;

        var index = path.LastIndexOf('/');
        return index <= 0 ? "/" : path[..index];
    }

    public static string GetFileName(string path)
    {
        if (path == "/")
            return "/";

        var index = path.LastIndexOf('/');
        return index < 0 ? path : path[(index + 1)..];
    }

    /// <summary>Kök dahil her üst klasör: "/var/www" için ("/", "/"), ("var", "/var"), ("www", "/var/www").</summary>
    public static IReadOnlyList<(string Name, string Path)> GetBreadcrumbs(string path)
    {
        var crumbs = new List<(string, string)> { ("/", "/") };
        var current = string.Empty;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current += "/" + segment;
            crumbs.Add((segment, current));
        }

        return crumbs;
    }

    /// <summary><paramref name="path"/>, <paramref name="ancestor"/> ile aynı veya onun altında mı.</summary>
    public static bool IsSameOrDescendant(string path, string ancestor) =>
        path == ancestor
        || ancestor == "/"
        || path.StartsWith(ancestor + "/", StringComparison.Ordinal);

    private static bool HasControlCharacter(string value) => value.Any(char.IsControl);
}
