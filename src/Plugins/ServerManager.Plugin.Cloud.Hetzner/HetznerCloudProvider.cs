using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServerManager.Application.Cloud;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.Interfaces.Cloud;

namespace ServerManager.Plugin.Cloud.Hetzner;

public sealed class HetznerCloudProvider : ICloudProvider
{
    private const int PageSize = 50;
    private const int MaxPages = 20;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<HetznerOptions> _options;

    public HetznerCloudProvider(IHttpClientFactory httpClientFactory, IOptions<HetznerOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string SystemName => HetznerPlugin.SystemName;

    public string DisplayName => HetznerPlugin.DisplayName;

    public string TokenHelp => "Hetzner Cloud Console → proje → Security → API tokens bölümünden oluşturun. Yalnızca listeleme için 'Read', sunucu oluşturmak için 'Read & Write' yetkisi gerekir.";

    public string Currency => HetznerPlugin.Currency;

    public async Task<ServiceResult<string>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync(token, "servers?per_page=1", cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<string>.Failure(response.Message!);

        using var document = response.Data!;
        var total = document.RootElement.TryGetProperty("meta", out var meta)
            && meta.TryGetProperty("pagination", out var pagination)
            && pagination.TryGetProperty("total_entries", out var entries)
            && entries.ValueKind == JsonValueKind.Number
                ? entries.GetInt32()
                : (int?)null;
        return ServiceResult<string>.Success(total is null ? "Proje API anahtarı" : $"Proje API anahtarı · {total} sunucu");
    }

    public async Task<ServiceResult<IReadOnlyList<CloudServerInfo>>> ListServersAsync(string token, CancellationToken cancellationToken = default)
    {
        var items = await GetAllPagesAsync(token, "servers", "servers", cancellationToken);
        return items.IsSuccess
            ? ServiceResult<IReadOnlyList<CloudServerInfo>>.Success(items.Data!.Select(ParseServer).ToList())
            : ServiceResult<IReadOnlyList<CloudServerInfo>>.Failure(items.Message!);
    }

    public async Task<ServiceResult<CloudCatalog>> GetCatalogAsync(string token, CancellationToken cancellationToken = default)
    {
        var locations = await GetAllPagesAsync(token, "locations", "locations", cancellationToken);
        if (!locations.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(locations.Message!);

        var types = await GetAllPagesAsync(token, "server_types", "server_types", cancellationToken);
        if (!types.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(types.Message!);

        var images = await GetAllPagesAsync(token, "images?type=system&status=available", "images", cancellationToken);
        if (!images.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(images.Message!);

        var regions = locations.Data!
            .Select(l => new CloudOption(Text(l, "name")!, $"{Text(l, "city") ?? Text(l, "name")} ({Text(l, "name")})", Text(l, "description")))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sizes = types.Data!
            .Where(t => !IsDeprecated(t))
            .Select(ParseServerType)
            .Where(o => o.Regions is { Count: > 0 })
            .OrderBy(o => o.MonthlyPrice ?? decimal.MaxValue)
            .ToList();

        var systemImages = images.Data!
            .Where(i => !IsDeprecated(i) && Text(i, "name") is not null)
            .GroupBy(i => Text(i, "name")!)
            .Select(g =>
            {
                var architectures = g.Select(i => Text(i, "architecture")).Where(a => a is not null).Distinct().ToList();
                var description = Text(g.First(), "description") ?? g.Key;
                return new CloudOption(g.Key, description, architectures.Count == 0 ? null : string.Join(", ", architectures));
            })
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return ServiceResult<CloudCatalog>.Success(new CloudCatalog(regions, sizes, systemImages, Currency));
    }

    public async Task<ServiceResult<CloudCreateResult>> CreateServerAsync(string token, CloudCreateServerRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = request.Name,
            ["server_type"] = request.Size,
            ["image"] = request.Image,
            ["location"] = request.Region,
            ["start_after_create"] = true
        };
        if (!string.IsNullOrEmpty(request.UserData))
            payload["user_data"] = request.UserData;

        var response = await SendAsync(token, HttpMethod.Post, "servers", JsonContent.Create(payload), cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<CloudCreateResult>.Failure(response.Message!);

        using var document = response.Data!;
        if (!document.RootElement.TryGetProperty("server", out var server))
            return ServiceResult<CloudCreateResult>.Failure("Hetzner beklenmeyen bir yanıt döndürdü.");

        var rootPassword = Text(document.RootElement, "root_password");
        return ServiceResult<CloudCreateResult>.Success(new CloudCreateResult(ParseServer(server), rootPassword));
    }

    internal static CloudServerInfo ParseServer(JsonElement server)
    {
        string? ip = null;
        if (server.TryGetProperty("public_net", out var net) && net.TryGetProperty("ipv4", out var ipv4) && ipv4.ValueKind == JsonValueKind.Object)
            ip = Text(ipv4, "ip");

        string? location = null;
        if (server.TryGetProperty("datacenter", out var datacenter) && datacenter.ValueKind == JsonValueKind.Object
            && datacenter.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
            location = Text(loc, "name");

        string? size = null;
        decimal? price = null;
        if (server.TryGetProperty("server_type", out var type) && type.ValueKind == JsonValueKind.Object)
        {
            size = Text(type, "name");
            price = MonthlyPrice(type, location);
        }

        return new CloudServerInfo(
            server.GetProperty("id").ToString(),
            Text(server, "name") ?? string.Empty,
            Text(server, "status") ?? "unknown",
            ip,
            location,
            size,
            price,
            price is null ? null : HetznerPlugin.Currency,
            DateTime.TryParse(Text(server, "created"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created) ? created : null);
    }

    /// <summary>KDV hariç (net) aylık fiyat; konum verilmezse en düşük fiyat.</summary>
    internal static decimal? MonthlyPrice(JsonElement serverType, string? location)
    {
        if (!serverType.TryGetProperty("prices", out var prices) || prices.ValueKind != JsonValueKind.Array)
            return null;

        decimal? lowest = null;
        foreach (var entry in prices.EnumerateArray())
        {
            if (!entry.TryGetProperty("price_monthly", out var monthly) || !decimal.TryParse(Text(monthly, "net"), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                continue;

            value = decimal.Round(value, 2);
            if (location is not null && Text(entry, "location") == location)
                return value;
            lowest = lowest is null ? value : Math.Min(lowest.Value, value);
        }

        return location is null ? lowest : null;
    }

    private static CloudOption ParseServerType(JsonElement type)
    {
        var name = Text(type, "name")!;
        var cores = type.TryGetProperty("cores", out var c) ? c.ToString() : "?";
        var memory = type.TryGetProperty("memory", out var m) ? m.ToString() : "?";
        var disk = type.TryGetProperty("disk", out var d) ? d.ToString() : "?";
        var regions = type.TryGetProperty("prices", out var prices) && prices.ValueKind == JsonValueKind.Array
            ? prices.EnumerateArray().Select(p => Text(p, "location")).Where(l => l is not null).Select(l => l!).ToList()
            : [];
        return new CloudOption(name, $"{name.ToUpperInvariant()} · {cores} vCPU · {memory} GB RAM · {disk} GB disk", Text(type, "architecture"), MonthlyPrice(type, null), regions);
    }

    private static bool IsDeprecated(JsonElement element) =>
        (element.TryGetProperty("deprecated", out var deprecated) && deprecated.ValueKind is JsonValueKind.True or JsonValueKind.String)
        || (element.TryGetProperty("deprecation", out var deprecation) && deprecation.ValueKind == JsonValueKind.Object);

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<ServiceResult<List<JsonElement>>> GetAllPagesAsync(string token, string path, string property, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var separator = path.Contains('?') ? '&' : '?';
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await GetAsync(token, $"{path}{separator}page={page}&per_page={PageSize}", cancellationToken);
            if (!response.IsSuccess)
                return ServiceResult<List<JsonElement>>.Failure(response.Message!);

            using var document = response.Data!;
            if (document.RootElement.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
                items.AddRange(array.EnumerateArray().Select(e => e.Clone()));

            var next = document.RootElement.TryGetProperty("meta", out var meta)
                && meta.TryGetProperty("pagination", out var pagination)
                && pagination.TryGetProperty("next_page", out var nextPage)
                && nextPage.ValueKind == JsonValueKind.Number;
            if (!next)
                break;
        }

        return ServiceResult<List<JsonElement>>.Success(items);
    }

    private Task<ServiceResult<JsonDocument>> GetAsync(string token, string path, CancellationToken cancellationToken) =>
        SendAsync(token, HttpMethod.Get, path, null, cancellationToken);

    private async Task<ServiceResult<JsonDocument>> SendAsync(string token, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HetznerPlugin.HttpClientName);
        using var request = new HttpRequestMessage(method, new Uri(new Uri(_options.Value.ApiUrl), path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ServiceResult<JsonDocument>.Failure(ErrorMessage(response.StatusCode, body));

            return ServiceResult<JsonDocument>.Success(JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<JsonDocument>.Failure("Hetzner API zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return ServiceResult<JsonDocument>.Failure("Hetzner API'ye bağlanılamadı.");
        }
        catch (JsonException)
        {
            return ServiceResult<JsonDocument>.Failure("Hetzner beklenmeyen bir yanıt döndürdü.");
        }
    }

    internal static string ErrorMessage(HttpStatusCode status, string body) =>
        CloudApiErrorMessage.Create(status, body, "Hetzner",
            "API anahtarının bu işlem için yetkisi yok (sunucu oluşturmak için 'Read & Write' gerekir).",
            root => root.TryGetProperty("error", out var error) ? Text(error, "message") : null);
}
