using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Kod gerektirmeyen (bildirimsel) şablonlar: eklenti klasöründeki <c>templates/*.json</c> dosyaları. Dosya tek bir şablon
/// nesnesi veya <c>{ "categories": [...], "templates": [...] }</c> biçiminde olabilir. Bağlantı adresi ve ortam
/// değişkenlerinde <c>{{host}}</c>, <c>{{port}}</c>, <c>{{username}}</c>, <c>{{password}}</c>, <c>{{database}}</c>,
/// <c>{{encryptionKey}}</c> yer tutucuları kullanılır; <c>:url</c> (yüzde kodlama) ve <c>:ado</c> (ADO.NET değeri) biçimleyicileri
/// eklenebilir (ör. <c>{{password:url}}</c>). Şema: <c>docs/schemas/service-template.schema.json</c>.
/// </summary>
public static partial class ServiceTemplateJson
{
    public const string FolderName = "templates";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    private static readonly HashSet<string> CredentialPlaceholders = new(StringComparer.Ordinal) { "username", "password", "database", "encryptionKey" };
    private static readonly HashSet<string> EndpointPlaceholders = new(StringComparer.Ordinal) { "host", "port" };

    /// <summary>JSON metnini okur. Dosya okunamazsa <see cref="ServiceTemplateJsonResult.Errors"/> dolu, şablon listesi boş döner.</summary>
    public static ServiceTemplateJsonResult Parse(string json, string sourceName)
    {
        var errors = new List<string>();
        var templates = new List<ServiceTemplate>();
        var categories = new List<ServiceTemplateCategory>();

        List<ServiceTemplateJsonModel> models;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Failed(sourceName, "kök öğe bir JSON nesnesi olmalı.");

            if (HasProperty(document.RootElement, "templates"))
            {
                var file = document.RootElement.Deserialize<ServiceTemplateJsonFile>(Options) ?? new ServiceTemplateJsonFile();
                models = file.Templates ?? [];
                foreach (var category in file.Categories ?? [])
                {
                    if (string.IsNullOrWhiteSpace(category.Key) || string.IsNullOrWhiteSpace(category.DisplayName))
                        errors.Add($"{sourceName}: categories öğesinde key ve displayName zorunludur.");
                    else
                        categories.Add(new ServiceTemplateCategory(category.Key, category.DisplayName, category.Order ?? 100));
                }
            }
            else
            {
                models = [document.RootElement.Deserialize<ServiceTemplateJsonModel>(Options)!];
            }
        }
        catch (JsonException ex)
        {
            return Failed(sourceName, ex.Message);
        }

        foreach (var model in models)
        {
            try
            {
                templates.Add(ToTemplate(model));
            }
            catch (FormatException ex)
            {
                errors.Add($"{sourceName} ({model?.Key ?? "?"}): {ex.Message}");
            }
        }

        return new ServiceTemplateJsonResult(templates, categories, errors);

        static ServiceTemplateJsonResult Failed(string source, string message) =>
            new([], [], [$"{source}: JSON okunamadı: {message}"]);
    }

    /// <summary>Model → şablon. Zorunlu alan eksikse veya bilinmeyen yer tutucu varsa <see cref="FormatException"/> fırlatır.</summary>
    public static ServiceTemplate ToTemplate(ServiceTemplateJsonModel model)
    {
        if (model is null)
            throw new FormatException("şablon boş.");

        var connection = Compile(model.ConnectionString, "connectionString", allowEndpoint: true);
        var suggested = (model.SuggestedEnvironment ?? [])
            .Select(pair => (pair.Key, Value: Compile(pair.Value ?? string.Empty, $"suggestedEnvironment.{pair.Key}", allowEndpoint: true)!))
            .ToList();
        var environment = (model.Environment ?? [])
            .Select(e => (Entry: e, Value: Compile(e.Value ?? string.Empty, $"environment.{e.Key}", allowEndpoint: false)!))
            .ToList();
        foreach (var entry in model.DefaultEnvironment ?? [])
        {
            if (entry.Value is not null && PlaceholderPattern().IsMatch(entry.Value))
                throw new FormatException($"defaultEnvironment.{entry.Key}: yer tutucu kullanılamaz; kimlik bilgisi değişkenleri environment alanına yazılır.");
        }

        var dataPath = model.DataPath;
        var credentials = model.Credentials ?? new ServiceTemplateJsonCredentials();
        return new ServiceTemplate
        {
            Key = Required(model.Key, "key"),
            DisplayName = Required(model.DisplayName, "displayName"),
            Category = model.Category ?? throw new FormatException("category zorunludur (database veya application)."),
            CategoryKey = model.Group,
            Description = Required(model.Description, "description"),
            LogoFile = model.Logo ?? string.Empty,
            Color = model.Color ?? "#64748B",
            Image = Required(model.Image, "image"),
            Tags = model.Tags ?? [],
            Ports = (model.Ports ?? [])
                .Select(p => new ServicePortDefinition(
                    p.Name ?? string.Empty,
                    p.ContainerPort,
                    p.Role ?? ServicePortRole.Primary,
                    p.Label ?? string.Empty,
                    p.PublishByDefault ?? true))
                .ToList(),
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = credentials.Username ?? ServiceUsernameKind.None,
                DefaultUsername = credentials.DefaultUsername,
                UsernameLabel = credentials.UsernameLabel ?? "Kullanıcı adı",
                PasswordPolicy = credentials.Password ?? ServicePasswordPolicy.None,
                HasDatabase = credentials.Database ?? false,
                DefaultDatabase = credentials.DefaultDatabase,
                ReservedUsernames = credentials.ReservedUsernames ?? [],
                GeneratesEncryptionKey = credentials.GenerateEncryptionKey ?? false
            },
            DataPath = _ => dataPath,
            DataOwner = model.DataOwner,
            Environment = c => environment
                .Select(e => new ServiceEnvironmentValue(e.Entry.Key ?? string.Empty, e.Value(null, c), e.Entry.Secret ?? false))
                .ToList(),
            DefaultEnvironment = (model.DefaultEnvironment ?? [])
                .Select(e => new ServiceEnvironmentValue(e.Key ?? string.Empty, e.Value ?? string.Empty, e.Secret ?? false))
                .ToList(),
            Command = model.Command ?? [],
            HealthCommand = model.HealthCommand,
            ReadinessCommand = model.ReadinessCommand,
            ConsoleCommand = model.ConsoleCommand,
            ConsoleLabel = model.ConsoleLabel,
            ConnectionString = connection is null ? (_, _) => null : (e, c) => connection(e, c),
            SuggestedEnvironment = (e, c) => suggested.ToDictionary(pair => pair.Key, pair => pair.Value(e, c), StringComparer.Ordinal),
            RequiresX86 = model.RequiresX86 ?? false,
            MinMemoryMb = model.MinMemoryMb ?? 0,
            MemoryHint = model.MemoryHint,
            WarnOnMajorUpgrade = model.WarnOnMajorUpgrade ?? false,
            HealthTimeoutSeconds = model.HealthTimeoutSeconds ?? 180,
            Notes = model.Notes
        };
    }

    /// <summary>Yer tutuculu metni derler; bilinmeyen yer tutucu veya biçimleyici <see cref="FormatException"/> fırlatır.</summary>
    private static Func<ServiceEndpoint?, ServiceCredentials, string>? Compile(string? text, string field, bool allowEndpoint)
    {
        if (text is null)
            return null;

        foreach (Match match in PlaceholderPattern().Matches(text))
        {
            var name = match.Groups["name"].Value;
            if (!CredentialPlaceholders.Contains(name) && !(allowEndpoint && EndpointPlaceholders.Contains(name)))
            {
                throw new FormatException(allowEndpoint || !EndpointPlaceholders.Contains(name)
                    ? $"{field}: bilinmeyen yer tutucu {{{{{name}}}}}."
                    : $"{field}: {{{{{name}}}}} burada kullanılamaz (yalnızca bağlantı adresinde ve önerilen değişkenlerde).");
            }
        }

        if (text.Contains("{{", StringComparison.Ordinal) && PlaceholderPattern().Replace(text, string.Empty).Contains("{{", StringComparison.Ordinal))
            throw new FormatException($"{field}: yer tutucu biçimi hatalı; {{{{ad}}}} veya {{{{ad:url}}}} kullanın.");

        return (endpoint, credentials) => PlaceholderPattern().Replace(text, match =>
        {
            var raw = match.Groups["name"].Value switch
            {
                "host" => endpoint?.Host ?? string.Empty,
                "port" => endpoint?.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                "username" => credentials.Username ?? string.Empty,
                "password" => credentials.Password ?? string.Empty,
                "database" => credentials.Database ?? string.Empty,
                _ => credentials.EncryptionKey ?? string.Empty
            };
            return match.Groups["format"].Value switch
            {
                "url" => Uri.EscapeDataString(raw),
                "ado" => ServiceConnectionStrings.AdoValue(raw),
                _ => match.Groups["name"].Value == "host" ? HostForUrl(raw) : raw
            };
        });
    }

    private static string HostForUrl(string host) =>
        host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('[') ? $"[{host}]" : host;

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new FormatException($"{field} zorunludur.") : value;

    private static bool HasProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z]+)(?::(?<format>url|ado))?\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}

public sealed record ServiceTemplateJsonResult(
    IReadOnlyList<ServiceTemplate> Templates,
    IReadOnlyList<ServiceTemplateCategory> Categories,
    IReadOnlyList<string> Errors);

public sealed class ServiceTemplateJsonFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public List<ServiceTemplateJsonCategory>? Categories { get; set; }

    public List<ServiceTemplateJsonModel>? Templates { get; set; }
}

public sealed class ServiceTemplateJsonCategory
{
    public string? Key { get; set; }

    public string? DisplayName { get; set; }

    public int? Order { get; set; }
}

/// <summary>JSON şablonunun alanları; anlamları <see cref="ServiceTemplate"/> özellikleriyle aynıdır.</summary>
public sealed class ServiceTemplateJsonModel
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public string? Key { get; set; }

    public string? DisplayName { get; set; }

    public ManagedServiceCategory? Category { get; set; }

    /// <summary><see cref="ServiceTemplate.CategoryKey"/>.</summary>
    public string? Group { get; set; }

    public string? Description { get; set; }

    /// <summary><see cref="ServiceTemplate.LogoFile"/>.</summary>
    public string? Logo { get; set; }

    public string? Color { get; set; }

    public string? Image { get; set; }

    public List<string>? Tags { get; set; }

    public List<ServiceTemplateJsonPort>? Ports { get; set; }

    public ServiceTemplateJsonCredentials? Credentials { get; set; }

    public string? DataPath { get; set; }

    public string? DataOwner { get; set; }

    public List<ServiceTemplateJsonEnvironment>? Environment { get; set; }

    public List<ServiceTemplateJsonEnvironment>? DefaultEnvironment { get; set; }

    public List<string>? Command { get; set; }

    public string? HealthCommand { get; set; }

    public string? ReadinessCommand { get; set; }

    public string? ConsoleCommand { get; set; }

    public string? ConsoleLabel { get; set; }

    public string? ConnectionString { get; set; }

    public Dictionary<string, string>? SuggestedEnvironment { get; set; }

    public bool? RequiresX86 { get; set; }

    public int? MinMemoryMb { get; set; }

    public string? MemoryHint { get; set; }

    public bool? WarnOnMajorUpgrade { get; set; }

    public int? HealthTimeoutSeconds { get; set; }

    public string? Notes { get; set; }
}

public sealed class ServiceTemplateJsonPort
{
    public string? Name { get; set; }

    public int ContainerPort { get; set; }

    public ServicePortRole? Role { get; set; }

    public string? Label { get; set; }

    public bool? PublishByDefault { get; set; }
}

public sealed class ServiceTemplateJsonCredentials
{
    public ServiceUsernameKind? Username { get; set; }

    public string? DefaultUsername { get; set; }

    public string? UsernameLabel { get; set; }

    public ServicePasswordPolicy? Password { get; set; }

    public bool? Database { get; set; }

    public string? DefaultDatabase { get; set; }

    public List<string>? ReservedUsernames { get; set; }

    public bool? GenerateEncryptionKey { get; set; }
}

public sealed class ServiceTemplateJsonEnvironment
{
    public string? Key { get; set; }

    public string? Value { get; set; }

    public bool? Secret { get; set; }
}
