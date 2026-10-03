using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using ServerManager.Application.Plugins;

namespace ServerManager.Web.Framework.Plugins;

/// <summary>
/// <c>plugins/{systemname}/...</c> isteklerini eklenti klasöründeki <c>Content</c> dizinine yönlendirir. Web köküne
/// eklendiği için statik dosya sunumu, <c>asp-append-version</c> ve import map eklenti dosyalarını da görür.
/// </summary>
public sealed class PluginContentFileProvider : IFileProvider
{
    private readonly Dictionary<string, PhysicalFileProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public PluginContentFileProvider(IPluginCatalog catalog)
    {
        foreach (var plugin in catalog.Plugins.Where(p => p.IsLoaded))
        {
            var contentDirectory = Path.Combine(plugin.Directory, PluginContent.FolderName);
            if (Directory.Exists(contentDirectory))
                _providers[plugin.SystemName] = new PhysicalFileProvider(contentDirectory);
        }
    }

    public IFileInfo GetFileInfo(string subpath) =>
        TryResolve(subpath, out var provider, out var rest) ? provider.GetFileInfo(rest) : new NotFoundFileInfo(subpath);

    public IDirectoryContents GetDirectoryContents(string subpath) =>
        TryResolve(subpath, out var provider, out var rest) ? provider.GetDirectoryContents(rest) : NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) =>
        TryResolve(filter, out var provider, out var rest) ? provider.Watch(rest) : NullChangeToken.Singleton;

    private bool TryResolve(string subpath, out PhysicalFileProvider provider, out string rest)
    {
        provider = null!;
        rest = string.Empty;

        var segments = subpath.TrimStart('/').Split('/', 3);
        if (segments.Length < 2 || !string.Equals(segments[0], PluginContent.RootSegment, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!_providers.TryGetValue(segments[1], out var found))
            return false;

        provider = found;
        rest = segments.Length == 3 ? segments[2] : string.Empty;
        return true;
    }
}
