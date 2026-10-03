using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using ServerManager.Application.Plugins;

namespace ServerManager.Web.Framework.Plugins;

/// <summary>
/// Eklenti dizinindeki her alt klasörün <c>plugin.json</c> dosyasını okur ve assembly'sini varsayılan yükleme bağlamına
/// yükler; böylece eklenti çekirdek tiplerini (servisler, DTO'lar, arayüzler) uygulamayla ortak kullanır.
/// </summary>
public static partial class PluginLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static PluginLoadResult Load(string pluginsDirectory)
    {
        var plugins = new List<LoadedPlugin>();
        var startups = new List<IPluginStartup>();
        if (!Directory.Exists(pluginsDirectory))
            return new PluginLoadResult(new PluginCatalog(plugins), startups);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in Directory.EnumerateDirectories(pluginsDirectory).Order(StringComparer.Ordinal))
        {
            var descriptorPath = Path.Combine(directory, PluginDescriptor.FileName);
            if (!File.Exists(descriptorPath))
                continue;

            var (plugin, pluginStartups) = LoadPlugin(directory, descriptorPath, seen);
            if (plugin.Assembly is not null && plugins.Any(p => p.Assembly == plugin.Assembly))
            {
                plugins.Add(Failed(plugin.Descriptor, directory, "Bu assembly başka bir eklenti tarafından kullanılıyor."));
                continue;
            }

            plugins.Add(plugin);
            startups.AddRange(pluginStartups);
        }

        return new PluginLoadResult(new PluginCatalog(plugins), startups);
    }

    private static (LoadedPlugin Plugin, IReadOnlyList<IPluginStartup> Startups) LoadPlugin(string directory, string descriptorPath, HashSet<string> seen)
    {
        PluginDescriptor descriptor;
        try
        {
            descriptor = JsonSerializer.Deserialize<PluginDescriptor>(File.ReadAllText(descriptorPath), JsonOptions)
                         ?? throw new JsonException("Dosya boş.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            var fallback = new PluginDescriptor { SystemName = Path.GetFileName(directory), FriendlyName = Path.GetFileName(directory) };
            return (Failed(fallback, directory, $"{PluginDescriptor.FileName} okunamadı: {ex.Message}"), []);
        }

        if (!SystemNamePattern().IsMatch(descriptor.SystemName))
            return (Failed(descriptor, directory, "SystemName yalnızca harf, rakam ve nokta içerebilir (örn. DevOps.Dokploy)."), []);

        if (!seen.Add(descriptor.SystemName))
            return (Failed(descriptor, directory, "Aynı SystemName ile başka bir eklenti zaten yüklü."), []);

        var assemblyPath = Path.GetFullPath(Path.Combine(directory, descriptor.AssemblyFileName));
        if (string.IsNullOrWhiteSpace(descriptor.AssemblyFileName)
            || !assemblyPath.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !File.Exists(assemblyPath))
        {
            return (Failed(descriptor, directory, $"Eklenti assembly'si bulunamadı: {descriptor.AssemblyFileName}"), []);
        }

        try
        {
            RegisterDependencyResolver(directory);
            var assembly = LoadAssembly(assemblyPath);
            var startups = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPluginStartup).IsAssignableFrom(t))
                .Select(t => (IPluginStartup)Activator.CreateInstance(t)!)
                .ToList();

            return (new LoadedPlugin(descriptor, directory, assembly, null), startups);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException
                                       or ReflectionTypeLoadException or MissingMethodException or TargetInvocationException)
        {
            var message = ex is ReflectionTypeLoadException typeLoad
                ? string.Join(" ", typeLoad.LoaderExceptions.Where(e => e is not null).Select(e => e!.Message).Distinct())
                : ex.Message;
            return (Failed(descriptor, directory, $"Eklenti yüklenemedi: {message}"), []);
        }
    }

    private static Assembly LoadAssembly(string assemblyPath)
    {
        var name = AssemblyName.GetAssemblyName(assemblyPath);
        var loaded = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), name));
        return loaded ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    }

    /// <summary>Uygulamada bulunmayan, yalnızca eklentinin kendi klasöründeki bağımlılıklar buradan çözülür.</summary>
    private static void RegisterDependencyResolver(string directory)
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(directory, $"{name.Name}.dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
    }

    private static LoadedPlugin Failed(PluginDescriptor descriptor, string directory, string error) =>
        new(descriptor, directory, null, error);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$")]
    private static partial Regex SystemNamePattern();
}
