using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;

namespace ServerManager.Web.Services;

/// <summary>
/// wwwroot/js altındaki ES modüllerini sürüm parametreli adreslere eşleyen import map üretir.
/// Sayfa giriş dosyası asp-append-version ile aynı adresi aldığı için her modül tek örnek yüklenir.
/// </summary>
public sealed class ScriptImportMap
{
    private const string ScriptRoot = "js";

    private readonly IWebHostEnvironment _environment;
    private readonly IFileVersionProvider _versionProvider;

    public ScriptImportMap(IWebHostEnvironment environment, IFileVersionProvider versionProvider)
    {
        _environment = environment;
        _versionProvider = versionProvider;
    }

    public string Build(PathString pathBase)
    {
        var imports = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var relativePath in EnumerateScripts(_environment.WebRootFileProvider, ScriptRoot))
        {
            var url = $"{pathBase}/{relativePath}";
            imports[url] = _versionProvider.AddFileVersionToPath(pathBase, url);
        }

        return JsonSerializer.Serialize(new { imports });
    }

    private static IEnumerable<string> EnumerateScripts(IFileProvider provider, string directory)
    {
        foreach (var entry in provider.GetDirectoryContents(directory))
        {
            var path = $"{directory}/{entry.Name}";
            if (entry.IsDirectory)
            {
                foreach (var nested in EnumerateScripts(provider, path))
                    yield return nested;
            }
            else if (entry.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }
}
