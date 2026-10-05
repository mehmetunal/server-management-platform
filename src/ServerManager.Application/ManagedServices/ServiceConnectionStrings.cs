using System.Globalization;

namespace ServerManager.Application.ManagedServices;

/// <summary>Bağlantı adresi biçimleri. Kullanıcı adı ve parola URL'lerde yüzde kodlamasıyla yazılır.</summary>
public static class ServiceConnectionStrings
{
    public static string Postgres(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"postgres://{UserInfo(credentials)}@{HostPort(endpoint)}/{Escape(credentials.Database)}";

    public static string MySql(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"mysql://{UserInfo(credentials)}@{HostPort(endpoint)}/{Escape(credentials.Database)}";

    public static string Redis(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"redis://:{Escape(credentials.Password)}@{HostPort(endpoint)}";

    public static string Mongo(ServiceEndpoint endpoint, ServiceCredentials credentials)
    {
        var database = string.IsNullOrEmpty(credentials.Database) ? string.Empty : Escape(credentials.Database);
        return $"mongodb://{UserInfo(credentials)}@{HostPort(endpoint)}/{database}?authSource=admin";
    }

    public static string Amqp(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"amqp://{UserInfo(credentials)}@{HostPort(endpoint)}/";

    public static string Http(ServiceEndpoint endpoint) => $"http://{HostPort(endpoint)}";

    /// <summary>ADO.NET biçimi (<c>Server=host,port;User Id=sa;Password=…;TrustServerCertificate=True</c>).</summary>
    public static string SqlServer(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"Server={endpoint.Host},{endpoint.Port.ToString(CultureInfo.InvariantCulture)};User Id={AdoValue(credentials.Username)};" +
        $"Password={AdoValue(credentials.Password)};TrustServerCertificate=True";

    /// <summary>Npgsql biçimi.</summary>
    public static string Npgsql(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"Host={endpoint.Host};Port={endpoint.Port.ToString(CultureInfo.InvariantCulture)};Database={AdoValue(credentials.Database)};" +
        $"Username={AdoValue(credentials.Username)};Password={AdoValue(credentials.Password)}";

    /// <summary>MySqlConnector / Pomelo biçimi.</summary>
    public static string MySqlAdo(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"Server={endpoint.Host};Port={endpoint.Port.ToString(CultureInfo.InvariantCulture)};Database={AdoValue(credentials.Database)};" +
        $"User ID={AdoValue(credentials.Username)};Password={AdoValue(credentials.Password)}";

    /// <summary>StackExchange.Redis biçimi.</summary>
    public static string RedisAdo(ServiceEndpoint endpoint, ServiceCredentials credentials) =>
        $"{HostPort(endpoint)},password={credentials.Password}";

    public static string HostPort(ServiceEndpoint endpoint)
    {
        var host = endpoint.Host.Contains(':', StringComparison.Ordinal) && !endpoint.Host.StartsWith('[') ? $"[{endpoint.Host}]" : endpoint.Host;
        return $"{host}:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string UserInfo(ServiceCredentials credentials) =>
        $"{Escape(credentials.Username)}:{Escape(credentials.Password)}";

    private static string Escape(string? value) => Uri.EscapeDataString(value ?? string.Empty);

    /// <summary>Noktalı virgül, süslü parantez veya baş/son boşluk içeren değer <c>{…}</c> içine alınır (iç <c>}</c> ikilenir).</summary>
    public static string AdoValue(string? value)
    {
        value ??= string.Empty;
        var needsQuote = value.IndexOfAny([';', '{', '}', '=', '\'', '"']) >= 0 || value != value.Trim();
        return needsQuote ? "{" + value.Replace("}", "}}", StringComparison.Ordinal) + "}" : value;
    }
}
