using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerManager.Application.ManagedServices;

/// <summary>Şablon portunun sunucuya yayını; <see cref="HostPort"/> boşsa port yalnızca Docker ağında erişilebilir.</summary>
public sealed record ServicePortBinding(int ContainerPort, int? HostPort);

/// <summary>Sunucuda yayınlanan port (docker run -p bind:host:container).</summary>
public sealed record PublishedPort(string BindAddress, int HostPort, int ContainerPort, string Name);

public static class ServicePortBindings
{
    public const string LoopbackAddress = "127.0.0.1";
    public const string AnyAddress = "0.0.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string ToJson(IEnumerable<ServicePortBinding> bindings) => JsonSerializer.Serialize(bindings.ToList(), JsonOptions);

    public static IReadOnlyList<ServicePortBinding> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ServicePortBinding>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Şablon sırasına göre yayınlanan portlar; şablonda olmayan container portları yok sayılır.</summary>
    public static IReadOnlyList<PublishedPort> Published(ServiceTemplate template, IEnumerable<ServicePortBinding> bindings, bool exposePublicly)
    {
        var map = bindings.Where(b => b.HostPort is not null).GroupBy(b => b.ContainerPort).ToDictionary(g => g.Key, g => g.First().HostPort!.Value);
        var bind = exposePublicly ? AnyAddress : LoopbackAddress;
        return template.Ports
            .Where(p => map.ContainsKey(p.ContainerPort))
            .Select(p => new PublishedPort(bind, map[p.ContainerPort], p.ContainerPort, p.Name))
            .ToList();
    }
}
