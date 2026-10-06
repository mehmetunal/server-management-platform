using System.Globalization;
using System.Text.RegularExpressions;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Şablon doğrulaması. Eklenti şablonları yüklenirken bu kurallardan geçer; hatalı şablon listeye alınmaz ve hatası
/// Eklentiler sayfasında gösterilir. Kurallar yerleşik şablonlar için de geçerlidir (testler bunu doğrular).
/// </summary>
public static partial class ServiceTemplateValidator
{
    /// <summary>Kalıcı anahtar uzunluğu sınırı (ManagedServices.TemplateKey sütunu).</summary>
    public const int MaxKeyLength = 96;
    public const int MaxTags = 20;
    public const int MaxPorts = 10;
    public const int MaxCommandArguments = 32;
    public const int MaxArgumentLength = 1024;
    public const int MaxShellCommandLength = 2000;
    public const int MaxEnvironmentValues = 50;
    public const int MinHealthTimeoutSeconds = 30;
    public const int MaxHealthTimeoutSeconds = 1800;

    private static readonly ServiceCredentials SampleCredentials = new()
    {
        Username = "sample_user",
        Password = "Sample-Passw0rd-123",
        Database = "sample_db",
        EncryptionKey = "0123456789abcdef0123456789abcdef"
    };

    private static readonly ServiceEndpoint SampleEndpoint = new("sm-svc-sample", 1234);

    /// <summary>Eklenti şablon anahtarlarının zorunlu öneki: <c>&lt;systemname küçük harf&gt;.</c></summary>
    public static string KeyPrefix(string pluginSystemName) => pluginSystemName.ToLowerInvariant() + ".";

    /// <summary>Şablon kurallara uyuyorsa boş liste, uymuyorsa Türkçe hata iletileri döner.</summary>
    /// <param name="template">Doğrulanacak şablon.</param>
    /// <param name="pluginSystemName">Eklenti şablonuysa eklentinin SystemName değeri; yerleşik şablonlar için null.</param>
    /// <param name="categoryKeys">Geçerli grup anahtarları; null ise yalnızca yerleşik gruplar.</param>
    public static IReadOnlyList<string> Validate(ServiceTemplate template, string? pluginSystemName = null, IReadOnlySet<string>? categoryKeys = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        var errors = new List<string>();
        try
        {
            ValidateCore(template, pluginSystemName, categoryKeys, errors);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Şablonun fonksiyonları (DataPath, Environment …) eklenti kodudur; hata fırlatırsa şablon geçersiz sayılır.
            errors.Add($"Şablon değerlendirilirken hata oluştu: {ex.GetType().Name}: {ex.Message}");
        }

        return errors;
    }

    private static void ValidateCore(ServiceTemplate template, string? pluginSystemName, IReadOnlySet<string>? categoryKeys, List<string> errors)
    {
        // Anahtar
        if (string.IsNullOrEmpty(template.Key))
        {
            errors.Add("Key boş olamaz.");
        }
        else if (pluginSystemName is null)
        {
            if (!BuiltInKeyPattern().IsMatch(template.Key))
                errors.Add($"Key \"{template.Key}\" geçersiz: küçük harf, rakam ve tire (en fazla 32 karakter).");
        }
        else
        {
            var prefix = KeyPrefix(pluginSystemName);
            if (!template.Key.StartsWith(prefix, StringComparison.Ordinal))
                errors.Add($"Key \"{template.Key}\" eklenti önekiyle başlamalı: \"{prefix}<ad>\".");
            else if (!LocalKeyPattern().IsMatch(template.Key[prefix.Length..]))
                errors.Add($"Key \"{template.Key}\" geçersiz: önekten sonraki ad küçük harf/rakamla başlamalı; küçük harf, rakam ve tire içerebilir (en fazla 40 karakter).");

            if (template.Key.Length > MaxKeyLength)
                errors.Add($"Key en fazla {MaxKeyLength.ToString(CultureInfo.InvariantCulture)} karakter olabilir.");
        }

        // Görünüm
        RequireText(template.DisplayName, "DisplayName", 64, errors);
        RequireText(template.Description, "Description", 500, errors);
        if (!Enum.IsDefined(template.Category))
            errors.Add("Category geçersiz (Database veya Application olmalı).");

        if (template.CategoryKey is not null)
        {
            var known = categoryKeys ?? ServiceTemplateCategories.BuiltIn.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
            if (!known.Contains(template.CategoryKey))
                errors.Add($"CategoryKey \"{template.CategoryKey}\" tanımlı bir grup değil.");
        }

        if (!string.IsNullOrEmpty(template.LogoFile) && !LogoPattern().IsMatch(template.LogoFile))
            errors.Add("LogoFile yalnızca dosya adı olabilir (harf, rakam, nokta, tire, alt çizgi; .svg/.png/.webp).");

        if (template.Color is null || !ColorPattern().IsMatch(template.Color))
            errors.Add("Color #RRGGBB biçiminde olmalı.");

        // İmaj ve sürümler
        if (string.IsNullOrEmpty(template.Image) || template.Image.Length > 255 || !ImagePattern().IsMatch(template.Image))
            errors.Add($"Image \"{template.Image}\" geçerli bir Docker imaj adı değil (etiketsiz, küçük harf; ör. getmeili/meilisearch).");

        if (template.Tags is null || template.Tags.Count == 0)
            errors.Add("Tags en az bir sürüm içermeli.");
        else
        {
            if (template.Tags.Count > MaxTags)
                errors.Add($"Tags en fazla {MaxTags.ToString(CultureInfo.InvariantCulture)} sürüm içerebilir.");
            foreach (var tag in template.Tags.Where(t => !ServiceValidation.IsValidTag(t)))
                errors.Add($"Sürüm etiketi \"{tag}\" geçersiz.");
            if (template.Tags.Distinct(StringComparer.Ordinal).Count() != template.Tags.Count)
                errors.Add("Tags tekrarlanan sürüm içeriyor.");
        }

        // Portlar
        var ports = template.Ports ?? [];
        if (ports.Count > MaxPorts)
            errors.Add($"Ports en fazla {MaxPorts.ToString(CultureInfo.InvariantCulture)} port içerebilir.");
        foreach (var port in ports)
        {
            if (port is null)
            {
                errors.Add("Ports boş öğe içeremez.");
                continue;
            }

            if (port.ContainerPort is < 1 or > 65535)
                errors.Add($"Port {port.ContainerPort.ToString(CultureInfo.InvariantCulture)} 1–65535 aralığında olmalı.");
            if (string.IsNullOrEmpty(port.Name) || !PortNamePattern().IsMatch(port.Name))
                errors.Add($"Port adı \"{port.Name}\" geçersiz (küçük harfle başlar; küçük harf, rakam, tire; en fazla 16 karakter).");
            if (!Enum.IsDefined(port.Role))
                errors.Add($"Port \"{port.Name}\" rolü geçersiz (Primary veya WebUi).");
            RequireText(port.Label, $"Port \"{port.Name}\" etiketi", 64, errors);
        }

        if (ports.Where(p => p is not null).Select(p => p.ContainerPort).Distinct().Count() != ports.Count(p => p is not null))
            errors.Add("Aynı container portu iki kez tanımlanmış.");
        if (ports.Where(p => p is not null).Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != ports.Count(p => p is not null))
            errors.Add("Aynı port adı iki kez tanımlanmış.");

        // Kimlik bilgileri
        var credentials = template.Credentials ?? new ServiceCredentialSpec();
        if (!Enum.IsDefined(credentials.UsernameKind))
            errors.Add("Credentials.UsernameKind geçersiz.");
        if (!Enum.IsDefined(credentials.PasswordPolicy))
            errors.Add("Credentials.PasswordPolicy geçersiz.");
        if (credentials.UsernameKind == ServiceUsernameKind.Fixed && !ServiceValidation.IsValidUsername(credentials.DefaultUsername))
            errors.Add("Sabit kullanıcı adında (Fixed) DefaultUsername geçerli bir kullanıcı adı olmalı.");
        if (credentials.UsernameKind == ServiceUsernameKind.Name && credentials.DefaultUsername is not null && !ServiceValidation.IsValidUsername(credentials.DefaultUsername))
            errors.Add($"DefaultUsername \"{credentials.DefaultUsername}\" geçersiz.");
        if (credentials.HasDatabase && credentials.DefaultDatabase is not null && !ServiceValidation.IsValidDatabaseName(credentials.DefaultDatabase))
            errors.Add($"DefaultDatabase \"{credentials.DefaultDatabase}\" geçersiz.");

        // Veri klasörü ve sahiplik
        foreach (var tag in (template.Tags ?? []).Where(ServiceValidation.IsValidTag))
        {
            var path = template.DataPath(tag);
            if (path is not null && !IsSafeContainerPath(path))
                errors.Add($"DataPath \"{path}\" ({tag}) mutlak ve güvenli bir container yolu olmalı (ör. /var/lib/app; '..' ve boşluk içeremez).");
        }

        if (template.DataOwner is not null && !OwnerPattern().IsMatch(template.DataOwner))
            errors.Add("DataOwner uid veya uid:gid biçiminde olmalı (ör. 1000:1000).");

        // Ortam değişkenleri
        var environment = template.Environment(SampleCredentials) ?? [];
        ValidateEnvironment(environment, "Environment", errors);
        ValidateEnvironment(template.DefaultEnvironment ?? [], "DefaultEnvironment", errors);
        if (environment.Count > 0 && environment.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count() != environment.Count)
            errors.Add("Environment aynı değişkeni iki kez tanımlıyor.");
        foreach (var secret in environment.Where(e => !e.Secret && SampleCredentials.Secrets().Any(s => e.Value.Contains(s, StringComparison.Ordinal))))
            errors.Add($"Environment \"{secret.Key}\" parola veya anahtar içeriyor; Secret = true olmalı.");

        // Container argümanları ve container içi komutlar
        var command = template.Command ?? [];
        if (command.Count > MaxCommandArguments)
            errors.Add($"Command en fazla {MaxCommandArguments.ToString(CultureInfo.InvariantCulture)} argüman içerebilir.");
        foreach (var argument in command)
        {
            if (argument is null || argument.Length > MaxArgumentLength || argument.Contains('\0', StringComparison.Ordinal))
                errors.Add("Command argümanları boş olamaz, NUL içeremez ve en fazla 1024 karakter olabilir.");
        }

        ValidateShellCommand(template.HealthCommand, "HealthCommand", errors);
        ValidateShellCommand(template.ReadinessCommand, "ReadinessCommand", errors);
        ValidateShellCommand(template.ConsoleCommand, "ConsoleCommand", errors);
        if (pluginSystemName is not null && string.IsNullOrWhiteSpace(template.HealthCommand))
            errors.Add("Eklenti şablonlarında HealthCommand zorunludur (kurulum sağlık kontrolüyle doğrulanır).");
        if (template.ConsoleLabel is { Length: > 32 })
            errors.Add("ConsoleLabel en fazla 32 karakter olabilir.");

        // Bağlantı biçimleri değerlendirilebilmeli
        _ = template.ConnectionString(SampleEndpoint, SampleCredentials);
        var suggested = template.SuggestedEnvironment(SampleEndpoint, SampleCredentials) ?? new Dictionary<string, string>();
        foreach (var key in suggested.Keys.Where(k => !EnvironmentKeyPattern().IsMatch(k)))
            errors.Add($"SuggestedEnvironment anahtarı \"{key}\" geçersiz.");

        // Kaynak ipuçları
        if (template.MinMemoryMb is < 0 or > ServiceValidation.MaxMemoryLimitMb)
            errors.Add("MinMemoryMb 0 ile 1048576 arasında olmalı.");
        if (template.HealthTimeoutSeconds is < MinHealthTimeoutSeconds or > MaxHealthTimeoutSeconds)
            errors.Add($"HealthTimeoutSeconds {MinHealthTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}–{MaxHealthTimeoutSeconds.ToString(CultureInfo.InvariantCulture)} arasında olmalı.");
        if (template.Notes is { Length: > 1000 })
            errors.Add("Notes en fazla 1000 karakter olabilir.");
        if (template.MemoryHint is { Length: > 300 })
            errors.Add("MemoryHint en fazla 300 karakter olabilir.");
    }

    /// <summary>Kancanın döndürdüğü ek argümanları denetler; gizli değer içeren argüman reddedilir (docker inspect'te görünür).</summary>
    public static string? ValidateExtraArguments(IReadOnlyList<string>? arguments, IEnumerable<string> secrets)
    {
        if (arguments is null || arguments.Count == 0)
            return null;

        if (arguments.Count > MaxCommandArguments)
            return $"Eklenti en fazla {MaxCommandArguments.ToString(CultureInfo.InvariantCulture)} ek argüman verebilir.";

        var secretList = secrets.Where(s => s.Length >= 4).ToList();
        foreach (var argument in arguments)
        {
            if (string.IsNullOrEmpty(argument) || argument.Length > MaxArgumentLength || argument.IndexOfAny(['\0', '\r', '\n']) >= 0)
                return "Eklentinin ek argümanı boş, çok uzun veya satır sonu/NUL içeriyor.";
            if (secretList.Any(s => argument.Contains(s, StringComparison.Ordinal)))
                return "Eklentinin ek argümanı gizli bir değer içeriyor; gizli değerler yalnızca ortam dosyasıyla verilebilir.";
        }

        return null;
    }

    public static bool IsSafeContainerPath(string path) =>
        path.Length is > 1 and <= 255 && ContainerPathPattern().IsMatch(path) && !path.Split('/').Any(segment => segment is "." or "..");

    private static void ValidateEnvironment(IReadOnlyList<ServiceEnvironmentValue> values, string name, List<string> errors)
    {
        if (values.Count > MaxEnvironmentValues)
            errors.Add($"{name} en fazla {MaxEnvironmentValues.ToString(CultureInfo.InvariantCulture)} değişken içerebilir.");

        foreach (var value in values)
        {
            if (value is null || string.IsNullOrEmpty(value.Key) || !EnvironmentKeyPattern().IsMatch(value.Key))
            {
                errors.Add($"{name} değişken adı \"{value?.Key}\" geçersiz ([A-Za-z_][A-Za-z0-9_]*).");
                continue;
            }

            if (value.Value is null || value.Value.Length > ManagedServiceSettingsRules.MaxEnvironmentValueLength || value.Value.IndexOfAny(['\0', '\r', '\n']) >= 0)
                errors.Add($"{name} \"{value.Key}\" değeri satır sonu/NUL içeremez ve en fazla 4096 karakter olabilir.");
        }
    }

    private static void ValidateShellCommand(string? command, string name, List<string> errors)
    {
        if (command is null)
            return;

        if (string.IsNullOrWhiteSpace(command))
            errors.Add($"{name} boş olamaz (gerekmiyorsa null bırakın).");
        else if (command.Length > MaxShellCommandLength || command.IndexOfAny(['\0', '\r', '\n']) >= 0)
            errors.Add($"{name} tek satır olmalı, NUL içeremez ve en fazla {MaxShellCommandLength.ToString(CultureInfo.InvariantCulture)} karakter olabilir.");
    }

    private static void RequireText(string? value, string name, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{name} boş olamaz.");
        else if (value.Length > maxLength)
            errors.Add($"{name} en fazla {maxLength.ToString(CultureInfo.InvariantCulture)} karakter olabilir.");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}$")]
    private static partial Regex BuiltInKeyPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$")]
    private static partial Regex LocalKeyPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]{0,59}\.(svg|png|webp)$")]
    private static partial Regex LogoPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorPattern();

    /// <summary>Docker referans dilbilgisi (etiketsiz): isteğe bağlı kayıt sunucusu[:port]/ + küçük harfli yol bileşenleri.</summary>
    [GeneratedRegex(@"^(?:[a-z0-9]+(?:[.-][a-z0-9]+)*(?::[0-9]{1,5})?/)?[a-z0-9]+(?:(?:[._]|__|-+)[a-z0-9]+)*(?:/[a-z0-9]+(?:(?:[._]|__|-+)[a-z0-9]+)*)*$")]
    private static partial Regex ImagePattern();

    [GeneratedRegex("^[a-z][a-z0-9-]{0,15}$")]
    private static partial Regex PortNamePattern();

    [GeneratedRegex("^/[A-Za-z0-9._/-]+$")]
    private static partial Regex ContainerPathPattern();

    [GeneratedRegex(@"^\d{1,10}(:\d{1,10})?$")]
    private static partial Regex OwnerPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentKeyPattern();
}
