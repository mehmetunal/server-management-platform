using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;
using ServerManager.Application.Plugins;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Web.Services;

/// <summary>
/// wwwroot/js ve eklentilerin Content/js klasöründeki ES modüllerini sürüm parametreli adreslere eşleyen import map üretir.
/// Sayfa giriş dosyası asp-append-version ile aynı adresi aldığı için her modül tek örnek yüklenir. Eklentiler çekirdek
/// modüllere <c>@app/core/dom.js</c> gibi adlarla ulaşır; böylece uygulama bir alt yolda yayınlansa da adresler bozulmaz.
/// </summary>
public sealed class ScriptImportMap
{
    private const string ScriptRoot = "js";
    private const string AppAlias = "@app/";

    private readonly IWebHostEnvironment _environment;
    private readonly IFileVersionProvider _versionProvider;
    private readonly IPluginCatalog _pluginCatalog;

    public ScriptImportMap(IWebHostEnvironment environment, IFileVersionProvider versionProvider, IPluginCatalog pluginCatalog)
    {
        _environment = environment;
        _versionProvider = versionProvider;
        _pluginCatalog = pluginCatalog;
    }

    public string Build(PathString pathBase)
    {
        var provider = _environment.WebRootFileProvider;
        var imports = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var relativePath in EnumerateScripts(provider, ScriptRoot))
        {
            var url = $"{pathBase}/{relativePath}";
            var versioned = _versionProvider.AddFileVersionToPath(pathBase, url);
            imports[url] = versioned;
            imports[AppAlias + relativePath[(ScriptRoot.Length + 1)..]] = versioned;
        }

        foreach (var plugin in _pluginCatalog.Plugins.Where(p => p.IsLoaded))
        {
            foreach (var relativePath in EnumerateScripts(provider, $"{PluginContent.BasePath(plugin.SystemName)}/{ScriptRoot}"))
            {
                var url = $"{pathBase}/{relativePath}";
                imports[url] = _versionProvider.AddFileVersionToPath(pathBase, url);
            }
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
