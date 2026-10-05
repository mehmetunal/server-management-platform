using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using ServerManager.Application.Deployments;
using ServerManager.Application.Docker;

namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Servis formundaki değerlerin kuralları. Komutlara giren her değer ayrıca tek tırnakla kaçışlanır; bu kurallar ikinci
/// savunma hattıdır ve ortam dosyası, URL ve seçenek dosyası biçimlerinin bozulmamasını sağlar.
/// </summary>
public static partial class ServiceValidation
{
    public const int MaxAllowedSources = 50;
    public const int MaxExtraNetworks = 5;
    public const int MinMemoryLimitMb = 32;
    public const int MaxMemoryLimitMb = 1_048_576;
    public const decimal MinCpuLimit = 0.1m;
    public const decimal MaxCpuLimit = 256m;
    public const int MinStandardPasswordLength = 12;
    public const int MaxPasswordLength = 128;

    /// <summary>Docker etiket dilbilgisi: harf, rakam veya alt çizgiyle başlar; en fazla 128 karakter.</summary>
    public static bool IsValidTag(string? tag) => tag is not null && TagPattern().IsMatch(tag);

    public static bool IsValidUsername(string? value) => value is not null && UsernamePattern().IsMatch(value);

    public static bool IsValidEmail(string? value) => value is not null && value.Length <= 254 && EmailPattern().IsMatch(value);

    public static bool IsValidDatabaseName(string? value) => value is not null && DatabasePattern().IsMatch(value);

    /// <summary>
    /// Parola karakterleri: harf, rakam ve tırnak, ters bölü ve boşluk dışındaki semboller. Böylece değer ortam dosyasında,
    /// URL'de ve MySQL seçenek dosyasında kaçış gerektirmeden kullanılabilir.
    /// </summary>
    public static bool TryValidatePassword(ServicePasswordPolicy policy, string? password, out string? error)
    {
        error = null;
        if (policy == ServicePasswordPolicy.None)
            return true;

        if (string.IsNullOrEmpty(password))
        {
            error = "Parola zorunludur.";
            return false;
        }

        if (password.Length > MaxPasswordLength)
        {
            error = $"Parola en fazla {MaxPasswordLength} karakter olabilir.";
            return false;
        }

        if (!PasswordPattern().IsMatch(password))
        {
            error = "Parolada boşluk, tırnak (' \" `) ve ters bölü (\\) kullanılamaz.";
            return false;
        }

        if (policy == ServicePasswordPolicy.MssqlComplex)
        {
            var categories = (password.Any(char.IsAsciiLetterUpper) ? 1 : 0)
                             + (password.Any(char.IsAsciiLetterLower) ? 1 : 0)
                             + (password.Any(char.IsAsciiDigit) ? 1 : 0)
                             + (password.Any(c => !char.IsAsciiLetterOrDigit(c)) ? 1 : 0);
            if (password.Length < 8 || categories < 3)
            {
                error = "SQL Server parolası en az 8 karakter olmalı ve büyük harf, küçük harf, rakam, sembolden en az üçünü içermelidir.";
                return false;
            }

            return true;
        }

        if (password.Length < MinStandardPasswordLength)
        {
            error = $"Parola en az {MinStandardPasswordLength} karakter olmalıdır.";
            return false;
        }

        return true;
    }

    /// <summary>Sunucu portu 1-65535 aralığında olmalı; 1024 altı portlar yalnızca ayarda izin verilmişse kullanılabilir.</summary>
    public static bool TryValidateHostPort(int port, bool allowPrivileged, out string? error)
    {
        error = null;
        if (port is < 1 or > 65535)
        {
            error = "Port 1 ile 65535 arasında olmalıdır.";
            return false;
        }

        if (port < 1024 && !allowPrivileged)
        {
            error = "1024 altındaki portlar sistem servislerine ayrılmıştır; 1024 veya üstü bir port seçin.";
            return false;
        }

        return true;
    }

    /// <summary>Sunucu klasörü modu: mutlak, sade ve sistem klasörü olmayan bir yol (deploy klasörü kurallarıyla aynı).</summary>
    public static bool TryValidateHostPath(string? path, out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Veri klasörü zorunludur.";
            return false;
        }

        if (DeployPaths.TryValidate(path, out var deployError))
        {
            error = null;
            return true;
        }

        error = "Veri klasörü: " + (deployError ?? "geçersiz yol.");
        return false;
    }

    public static bool IsValidNetworkName(string? value) =>
        DockerNames.IsValidNetworkName(value) && value!.Length <= 64 && value is not "host" and not "none" and not "bridge";

    /// <summary>Virgül, boşluk veya satır sonuyla ayrılmış ağ adları; panel ağı ve vekil ağı listeden çıkarılır.</summary>
    public static bool TryParseNetworks(string? text, out IReadOnlyList<string> networks, out string? error)
    {
        networks = [];
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var result = new List<string>();
        foreach (var token in text.Split([',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsValidNetworkName(token))
            {
                error = $"\"{token}\" geçerli bir Docker ağ adı değil (host, none ve bridge kullanılamaz).";
                return false;
            }

            if (token is ManagedServiceNames.ServicesNetwork or ManagedServiceNames.ProxyNetwork || result.Contains(token, StringComparer.Ordinal))
                continue;

            result.Add(token);
        }

        if (result.Count > MaxExtraNetworks)
        {
            error = $"En fazla {MaxExtraNetworks} ek ağ seçilebilir.";
            return false;
        }

        networks = result;
        return true;
    }

    /// <summary>
    /// İzin verilen kaynak adresleri: IPv4/IPv6 adres veya CIDR; virgül, boşluk veya satır sonuyla ayrılır. Adresler ağ adresine
    /// normalleştirilir (10.0.0.5/24 → 10.0.0.0/24), tek adres /32 (IPv6 /128) olur. /0 kabul edilmez; herkese açmak için liste boş bırakılır.
    /// </summary>
    public static bool TryParseCidrs(string? text, out IReadOnlyList<string> cidrs, out string? error)
    {
        cidrs = [];
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var result = new List<string>();
        foreach (var token in text.Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryNormalizeCidr(token, out var normalized, out var tokenError))
            {
                error = tokenError;
                return false;
            }

            if (!result.Contains(normalized, StringComparer.Ordinal))
                result.Add(normalized);
        }

        if (result.Count > MaxAllowedSources)
        {
            error = $"En fazla {MaxAllowedSources} adres veya ağ girilebilir.";
            return false;
        }

        cidrs = result;
        return true;
    }

    public static bool TryNormalizeCidr(string token, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = $"\"{token}\" geçerli bir IP adresi veya CIDR değil (ör. 203.0.113.10, 10.0.0.0/24, 2001:db8::/32).";

        var parts = token.Split('/');
        if (parts.Length > 2 || parts[0].Length == 0 || parts[0].Length > 45)
            return false;

        var addressText = parts[0];
        var isV4Text = Ipv4Pattern().IsMatch(addressText);
        var isV6Text = addressText.Contains(':', StringComparison.Ordinal) && Ipv6CharsPattern().IsMatch(addressText);
        if ((!isV4Text && !isV6Text) || !IPAddress.TryParse(addressText, out var address))
            return false;

        var isV4 = address.AddressFamily == AddressFamily.InterNetwork;
        if (isV4 != isV4Text)
            return false;

        if (isV4 && addressText.Split('.').Any(octet => octet.Length > 1 && octet[0] == '0'))
            return false;

        var maxPrefix = isV4 ? 32 : 128;
        var prefix = maxPrefix;
        if (parts.Length == 2)
        {
            if (parts[1].Length is 0 or > 3 || !parts[1].All(char.IsAsciiDigit)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out prefix) || prefix > maxPrefix)
                return false;
        }

        if (prefix == 0)
        {
            error = "/0 (tüm adresler) kabul edilmez; herkese açmak için listeyi boş bırakın.";
            return false;
        }

        var bytes = address.GetAddressBytes();
        for (var bit = prefix; bit < bytes.Length * 8; bit++)
            bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));

        normalized = $"{new IPAddress(bytes)}/{prefix.ToString(CultureInfo.InvariantCulture)}";
        error = null;
        return true;
    }

    public static bool IsIpv6Cidr(string cidr) => cidr.Contains(':', StringComparison.Ordinal);

    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$")]
    private static partial Regex TagPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.-]{0,62}$")]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,62}$")]
    private static partial Regex DatabasePattern();

    [GeneratedRegex(@"^[A-Za-z0-9!#$%&()*+,\-./:;<=>?@\[\]^_{|}~]+$")]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}$")]
    private static partial Regex Ipv4Pattern();

    [GeneratedRegex("^[0-9A-Fa-f:.]+$")]
    private static partial Regex Ipv6CharsPattern();
}
