using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerManager.Application.ManagedServices;

/// <summary>Servisin kimlik bilgileri; veritabanında JSON olarak şifreli saklanır.</summary>
public sealed record ServiceCredentials
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? Database { get; init; }

    /// <summary>Şablonun ürettiği ek gizli değer (n8n şifreleme anahtarı).</summary>
    public string? EncryptionKey { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ServiceCredentials FromJson(string json) =>
        JsonSerializer.Deserialize<ServiceCredentials>(json, JsonOptions) ?? new ServiceCredentials();

    /// <summary>Loglarda maskelenecek değerler.</summary>
    public IEnumerable<string> Secrets()
    {
        if (!string.IsNullOrEmpty(Password))
            yield return Password;
        if (!string.IsNullOrEmpty(EncryptionKey))
            yield return EncryptionKey;
    }

    public override string ToString() =>
        $"ServiceCredentials {{ Username = {Username}, Database = {Database}, Password = {(Password is null ? "null" : "***")} }}";
}
