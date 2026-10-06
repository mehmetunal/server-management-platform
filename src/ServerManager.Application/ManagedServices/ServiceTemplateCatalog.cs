using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ServerManager.Application.Plugins;

namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Yerleşik şablonları ve eklenti şablonlarını birleştirir. Eklenti şablonları ilk kullanımda bir kez okunur ve doğrulanır;
/// hangi şablonun listelendiği ise her çağrıda eklentinin etkinlik durumuna göre belirlenir.
/// </summary>
public sealed partial class ServiceTemplateCatalog : IServiceTemplateCatalog
{
    private const string PluginContentFolder = "Content";

    private readonly IPluginCatalog _plugins;
    private readonly IEnumerable<IServiceTemplateProvider> _providers;
    private readonly IEnumerable<IServiceTemplateHooks> _hooks;
    private readonly ILogger<ServiceTemplateCatalog> _logger;
    private readonly Lazy<State> _state;

    public ServiceTemplateCatalog(
        IPluginCatalog plugins,
        IEnumerable<IServiceTemplateProvider> providers,
        IEnumerable<IServiceTemplateHooks> hooks,
        ILogger<ServiceTemplateCatalog> logger)
    {
        _plugins = plugins;
        _providers = providers;
        _hooks = hooks;
        _logger = logger;
        _state = new Lazy<State>(Build, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Yalnızca yerleşik şablonlar (eklentisiz testler ve araçlar için).</summary>
    public static ServiceTemplateCatalog BuiltInOnly() =>
        new(new PluginCatalog([]), [], [], NullLogger<ServiceTemplateCatalog>.Instance);

    public IReadOnlyList<ServiceTemplateIssue> Issues => _state.Value.Issues;

    public IReadOnlyList<ServiceTemplate> GetAvailable() =>
        _state.Value.Ordered.Where(e => IsEnabled(e.Owner)).Select(e => e.Template).ToList();

    public IReadOnlyList<ServiceTemplateCategory> GetCategories()
    {
        var available = GetAvailable();
        return _state.Value.Categories
            .Where(c => IsEnabled(c.Owner) && available.Any(t => string.Equals(t.GroupKey, c.Category.Key, StringComparison.Ordinal)))
            .Select(c => c.Category)
            .OrderBy(c => c.Order)
            .ThenBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public ServiceTemplate? Find(string? key) =>
        key is not null && _state.Value.ByKey.TryGetValue(key, out var entry) && IsEnabled(entry.Owner) ? entry.Template : null;

    public ServiceTemplateResolution Resolve(string? key)
    {
        if (key is not null && _state.Value.ByKey.TryGetValue(key, out var entry))
        {
            return IsEnabled(entry.Owner)
                ? new ServiceTemplateResolution(entry.Template, ServiceTemplateAvailability.Available, entry.Owner?.SystemName, entry.Owner?.Descriptor.FriendlyName)
                : new ServiceTemplateResolution(entry.Template, ServiceTemplateAvailability.PluginDisabled, entry.Owner!.SystemName, entry.Owner.Descriptor.FriendlyName);
        }

        // Eklenti kaldırıldıysa anahtarın önekinden eklentiyi tahmin et (ör. services.extra.meilisearch → Services.Extra).
        var owner = key is null
            ? null
            : _plugins.Plugins
                .Where(p => key.StartsWith(ServiceTemplateValidator.KeyPrefix(p.SystemName), StringComparison.Ordinal))
                .OrderByDescending(p => p.SystemName.Length)
                .FirstOrDefault();
        var guessed = owner?.SystemName ?? GuessPluginName(key);
        return new ServiceTemplateResolution(null, ServiceTemplateAvailability.Missing, guessed, owner?.Descriptor.FriendlyName);
    }

    public IReadOnlyList<IServiceTemplateHooks> GetHooks(string templateKey)
    {
        var state = _state.Value;
        if (!state.Hooks.TryGetValue(templateKey, out var hooks) || !state.ByKey.TryGetValue(templateKey, out var entry) || !IsEnabled(entry.Owner))
            return [];

        return hooks;
    }

    public IReadOnlyList<string> GetIssues(string pluginSystemName) =>
        _state.Value.Issues
            .Where(i => string.Equals(i.PluginSystemName, pluginSystemName, StringComparison.OrdinalIgnoreCase))
            .Select(i => $"{i.Source}: {i.Message}")
            .ToList();

    private bool IsEnabled(LoadedPlugin? owner) => owner is null || _plugins.IsEnabled(owner.SystemName);

    private static string? GuessPluginName(string? key)
    {
        if (key is null)
            return null;

        var separator = key.LastIndexOf('.');
        return separator > 0 ? key[..separator] : null;
    }

    // ---------------------------------------------------------------- Yükleme

    private State Build()
    {
        var state = new State();
        foreach (var category in ServiceTemplateCategories.BuiltIn)
            state.Categories.Add(new CategoryEntry(category, null));
        foreach (var template in ServiceTemplates.BuiltIn)
            state.Add(new Entry(template, null));

        // Kaynakları eklentiye göre grupla: DI sağlayıcıları (assembly'den) ve templates/*.json dosyaları.
        var sources = new List<Source>();
        foreach (var provider in _providers)
        {
            var owner = _plugins.FindByAssembly(provider.GetType().Assembly);
            sources.Add(new Source(owner, provider.GetType().Name, () => Read(provider)));
        }

        foreach (var plugin in _plugins.Plugins.Where(p => p.IsLoaded && _plugins.Find(p.SystemName) == p))
        {
            var folder = Path.Combine(plugin.Directory, ServiceTemplateJson.FolderName);
            if (!Directory.Exists(folder))
                continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
            {
                var name = $"{ServiceTemplateJson.FolderName}/{Path.GetFileName(file)}";
                sources.Add(new Source(plugin, name, () => ServiceTemplateJson.Parse(File.ReadAllText(file), name)));
            }
        }

        foreach (var group in sources.GroupBy(s => s.Owner))
            LoadPlugin(state, group.Key, group.ToList());

        RegisterHooks(state);

        foreach (var issue in state.Issues)
            _logger.LogWarning("Servis şablonu atlandı/uyarı. Eklenti: {Plugin}, Kaynak: {Source}, Neden: {Reason}", issue.PluginSystemName ?? "(çekirdek)", issue.Source, issue.Message);

        _logger.LogInformation("Servis şablon kataloğu yüklendi: {BuiltIn} yerleşik, {Plugin} eklenti şablonu, {Issues} uyarı.",
            ServiceTemplates.BuiltIn.Count, state.Ordered.Count - ServiceTemplates.BuiltIn.Count, state.Issues.Count);
        return state;
    }

    private void LoadPlugin(State state, LoadedPlugin? owner, IReadOnlyList<Source> sources)
    {
        var systemName = owner?.SystemName;
        var results = new List<(Source Source, ServiceTemplateJsonResult Result)>();
        foreach (var source in sources)
        {
            try
            {
                results.Add((source, source.Read()));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                state.Issues.Add(new ServiceTemplateIssue(systemName, source.Name, $"Şablonlar okunamadı: {ex.Message}"));
            }
        }

        // Önce gruplar: şablonlar aynı eklentinin herhangi bir kaynağındaki gruba bağlanabilir.
        var categoryKeys = ServiceTemplateCategories.BuiltIn.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (source, result) in results)
        {
            foreach (var error in result.Errors)
                state.Issues.Add(new ServiceTemplateIssue(systemName, source.Name, error));

            foreach (var category in result.Categories)
            {
                var error = ValidateCategory(category, systemName);
                if (error is null && state.Categories.Any(c => string.Equals(c.Category.Key, category.Key, StringComparison.Ordinal)))
                    error = $"\"{category.Key}\" grubu zaten tanımlı.";

                if (error is not null)
                {
                    state.Issues.Add(new ServiceTemplateIssue(systemName, source.Name, error));
                    continue;
                }

                state.Categories.Add(new CategoryEntry(category, owner));
                categoryKeys.Add(category.Key);
            }
        }

        foreach (var (source, result) in results)
        {
            foreach (var template in result.Templates)
            {
                if (template is null)
                {
                    state.Issues.Add(new ServiceTemplateIssue(systemName, source.Name, "Boş şablon atlandı."));
                    continue;
                }

                var errors = ServiceTemplateValidator.Validate(template, systemName, categoryKeys);
                if (errors.Count > 0)
                {
                    state.Issues.Add(new ServiceTemplateIssue(systemName, source.Name, $"\"{template.Key}\" şablonu atlandı: {string.Join(" ", errors)}"));
                    continue;
                }

                if (state.ByKey.TryGetValue(template.Key, out var existing))
                {
                    var other = existing.Owner?.SystemName ?? "yerleşik şablonlar";
                    state.Issues.Add(new ServiceTemplateIssue(systemName, source.Name, $"\"{template.Key}\" şablonu atlandı: anahtar {other} ile çakışıyor."));
                    continue;
                }

                if (owner is not null)
                {
                    template.PluginSystemName = owner.SystemName;
                    if (!string.IsNullOrEmpty(template.LogoFile) && !File.Exists(Path.Combine(owner.Directory, PluginContentFolder, template.LogoFile)))
                        state.Issues.Add(new ServiceTemplateIssue(systemName, source.Name, $"\"{template.Key}\" logosu bulunamadı: {PluginContentFolder}/{template.LogoFile}"));
                }

                state.Add(new Entry(template, owner));
            }
        }
    }

    private void RegisterHooks(State state)
    {
        foreach (var hook in _hooks)
        {
            var owner = _plugins.FindByAssembly(hook.GetType().Assembly);
            string key;
            try
            {
                key = hook.TemplateKey;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                state.Issues.Add(new ServiceTemplateIssue(owner?.SystemName, hook.GetType().Name, $"Kanca okunamadı: {ex.Message}"));
                continue;
            }

            if (key is null || !state.ByKey.TryGetValue(key, out var entry))
            {
                state.Issues.Add(new ServiceTemplateIssue(owner?.SystemName, hook.GetType().Name, $"Kanca bilinmeyen \"{key}\" şablonuna bağlanmak istiyor; yok sayıldı."));
                continue;
            }

            if (entry.Owner != owner)
            {
                state.Issues.Add(new ServiceTemplateIssue(owner?.SystemName, hook.GetType().Name, $"Kanca yalnızca kendi eklentisinin şablonuna bağlanabilir (\"{key}\"); yok sayıldı."));
                continue;
            }

            if (!state.Hooks.TryGetValue(key, out var list))
                state.Hooks[key] = list = [];
            list.Add(hook);
        }
    }

    private static ServiceTemplateJsonResult Read(IServiceTemplateProvider provider) =>
        new(provider.GetTemplates() ?? [], provider.GetCategories() ?? [], []);

    private static string? ValidateCategory(ServiceTemplateCategory category, string? systemName)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.Key) || string.IsNullOrWhiteSpace(category.DisplayName))
            return "Grup anahtarı ve adı zorunludur.";
        if (category.DisplayName.Length > 64)
            return $"\"{category.Key}\" grup adı en fazla 64 karakter olabilir.";
        if (systemName is not null && !category.Key.StartsWith(ServiceTemplateValidator.KeyPrefix(systemName), StringComparison.Ordinal))
            return $"\"{category.Key}\" grup anahtarı eklenti önekiyle başlamalı: \"{ServiceTemplateValidator.KeyPrefix(systemName)}<ad>\".";
        if (!CategoryKeyPattern().IsMatch(category.Key))
            return $"\"{category.Key}\" grup anahtarı geçersiz (küçük harf, rakam, nokta ve tire).";
        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,95}$")]
    private static partial Regex CategoryKeyPattern();

    private sealed record Entry(ServiceTemplate Template, LoadedPlugin? Owner);

    private sealed record CategoryEntry(ServiceTemplateCategory Category, LoadedPlugin? Owner);

    private sealed record Source(LoadedPlugin? Owner, string Name, Func<ServiceTemplateJsonResult> Read);

    private sealed class State
    {
        public Dictionary<string, Entry> ByKey { get; } = new(StringComparer.Ordinal);

        public List<Entry> Ordered { get; } = [];

        public List<CategoryEntry> Categories { get; } = [];

        public Dictionary<string, List<IServiceTemplateHooks>> Hooks { get; } = new(StringComparer.Ordinal);

        public List<ServiceTemplateIssue> Issues { get; } = [];

        public void Add(Entry entry)
        {
            ByKey[entry.Template.Key] = entry;
            Ordered.Add(entry);
        }
    }
}
