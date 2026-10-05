using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ServerManager.Application.DTOs.ManagedServices;

namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Kurulum formu ve Ayarlar sekmesinin ortak kuralları. Hata alan adları formdaki input adlarıyla aynıdır
/// (<c>Ports[0].HostPort</c>, <c>Environment[2].Key</c> …); böylece hata ilgili alanın altında gösterilir.
/// </summary>
public static partial class ManagedServiceSettingsRules
{
    public const int MaxEnvironmentEntries = 100;
    public const int MaxEnvironmentValueLength = 4096;

    public static IEnumerable<(string Property, string Message)> Validate(ServiceTemplate template, ManagedServiceSettingsDto dto, bool allowPrivilegedPorts)
    {
        var seenContainerPorts = new HashSet<int>();
        var seenHostPorts = new HashSet<int>();
        for (var i = 0; i < dto.Ports.Count; i++)
        {
            var item = dto.Ports[i];
            if (template.Ports.All(p => p.ContainerPort != item.ContainerPort) || !seenContainerPorts.Add(item.ContainerPort))
            {
                yield return ($"Ports[{i}].ContainerPort", "Şablonda olmayan veya tekrarlanan port.");
                continue;
            }

            if (!item.Publish)
                continue;

            if (item.HostPort is not { } hostPort)
            {
                yield return ($"Ports[{i}].HostPort", "Yayınlanacak port için sunucu portu girin.");
                continue;
            }

            if (!ServiceValidation.TryValidateHostPort(hostPort, allowPrivilegedPorts, out var portError))
                yield return ($"Ports[{i}].HostPort", portError!);
            else if (!seenHostPorts.Add(hostPort))
                yield return ($"Ports[{i}].HostPort", "Aynı sunucu portu iki kez kullanılamaz.");
        }

        if (!ServiceValidation.TryParseCidrs(dto.AllowedSourceIps, out _, out var cidrError))
            yield return (nameof(ManagedServiceSettingsDto.AllowedSourceIps), cidrError!);

        if (dto.MemoryLimitMb is { } memory)
        {
            var minimum = Math.Max(ServiceValidation.MinMemoryLimitMb, template.MinMemoryMb);
            if (memory < minimum || memory > ServiceValidation.MaxMemoryLimitMb)
                yield return (nameof(ManagedServiceSettingsDto.MemoryLimitMb), $"{template.DisplayName} için bellek sınırı en az {minimum.ToString(CultureInfo.InvariantCulture)} MB olmalıdır.");
        }

        if (dto.CpuLimit is { } cpu && (cpu < ServiceValidation.MinCpuLimit || cpu > ServiceValidation.MaxCpuLimit || decimal.Round(cpu, 2) != cpu))
            yield return (nameof(ManagedServiceSettingsDto.CpuLimit), "CPU sınırı 0.1 ile 256 arasında, en fazla iki ondalıklı olmalıdır (ör. 0.5, 2).");

        if (!ServiceValidation.TryParseNetworks(dto.Networks, out _, out var networkError))
            yield return (nameof(ManagedServiceSettingsDto.Networks), networkError!);

        foreach (var error in ValidateEnvironment(template, dto.Environment))
            yield return error;
    }

    public static IEnumerable<(string Property, string Message)> ValidateEnvironment(ServiceTemplate template, IReadOnlyList<EnvironmentEntryDto> entries)
    {
        var reserved = ReservedKeys(template);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var key = entries[i].Key?.Trim();
            var value = entries[i].Value ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                if (value.Length > 0)
                    yield return ($"Environment[{i}].Key", "Değişken adı girin.");
                continue;
            }

            count++;
            if (key.Length > 128 || !EnvironmentKeyPattern().IsMatch(key))
                yield return ($"Environment[{i}].Key", "Ad harf veya alt çizgiyle başlamalı; harf, rakam ve alt çizgi içerebilir.");
            else if (reserved.Contains(key))
                yield return ($"Environment[{i}].Key", $"{key} servis tarafından yönetilir; kimlik bilgileri kurulumda belirlenir.");
            else if (!seen.Add(key))
                yield return ($"Environment[{i}].Key", $"{key} birden fazla kez tanımlanmış.");

            if (value.Length > MaxEnvironmentValueLength)
                yield return ($"Environment[{i}].Value", $"Değer en fazla {MaxEnvironmentValueLength.ToString(CultureInfo.InvariantCulture)} karakter olabilir.");
            else if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
                yield return ($"Environment[{i}].Value", "Değer satır sonu içeremez.");
        }

        if (count > MaxEnvironmentEntries)
            yield return (nameof(ManagedServiceSettingsDto.Environment), $"En fazla {MaxEnvironmentEntries.ToString(CultureInfo.InvariantCulture)} ek değişken tanımlanabilir.");
    }

    /// <summary>Kimlik bilgilerinden üretilen ve kullanıcının değiştiremeyeceği değişkenler.</summary>
    public static IReadOnlySet<string> ReservedKeys(ServiceTemplate template) =>
        template.Environment(new ServiceCredentials { Username = "x", Password = "x", Database = "x", EncryptionKey = "x" })
            .Select(e => e.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Formdaki ek değişkenler → <c>ANAHTAR=değer</c> satırları (boş satırlar atlanır); hiç yoksa null.</summary>
    public static string? ToEnvironmentText(IEnumerable<EnvironmentEntryDto> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            var key = entry.Key?.Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            builder.Append(key).Append('=').Append(entry.Value ?? string.Empty).Append('\n');
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    public static List<EnvironmentEntryDto> FromEnvironmentText(string? text)
    {
        var result = new List<EnvironmentEntryDto>();
        if (string.IsNullOrEmpty(text))
            return result;

        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
                continue;

            result.Add(new EnvironmentEntryDto { Key = line[..separator], Value = line[(separator + 1)..] });
        }

        return result;
    }

    /// <summary>
    /// Container'a verilen ortam dosyası: şablon varsayılanları, kullanıcının ek değişkenleri (varsayılanı ezer) ve
    /// kimlik bilgisi değişkenleri (ezilemez). Docker <c>--env-file</c> biçimi: değerler tırnaksız, satır başına bir değişken.
    /// </summary>
    public static string BuildEnvironmentFile(ServiceTemplate template, ServiceCredentials credentials, string? extraEnvironment)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();

        void Set(string key, string value)
        {
            if (!values.ContainsKey(key))
                order.Add(key);
            values[key] = value;
        }

        foreach (var item in template.DefaultEnvironment)
            Set(item.Key, item.Value);

        var reserved = ReservedKeys(template);
        foreach (var entry in FromEnvironmentText(extraEnvironment).Where(e => !reserved.Contains(e.Key!)))
            Set(entry.Key!, entry.Value ?? string.Empty);

        foreach (var item in template.Environment(credentials).Where(e => !string.IsNullOrEmpty(e.Value)))
            Set(item.Key, item.Value);

        var builder = new StringBuilder();
        foreach (var key in order)
            builder.Append(key).Append('=').Append(values[key].Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal)).Append('\n');

        return builder.ToString();
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentKeyPattern();
}
