using ServerManager.Application.Plugins;

namespace ServerManager.Web.Framework.Plugins;

public sealed record PluginLoadResult(PluginCatalog Catalog, IReadOnlyList<IPluginStartup> Startups);
