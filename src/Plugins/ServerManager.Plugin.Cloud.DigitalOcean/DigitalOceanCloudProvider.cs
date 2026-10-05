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

namespace ServerManager.Plugin.Cloud.DigitalOcean;

public sealed class DigitalOceanCloudProvider : ICloudProvider
{
    private const int PageSize = 100;
    private const int MaxPages = 20;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<DigitalOceanOptions> _options;

    public DigitalOceanCloudProvider(IHttpClientFactory httpClientFactory, IOptions<DigitalOceanOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string SystemName => DigitalOceanPlugin.SystemName;

    public string DisplayName => DigitalOceanPlugin.DisplayName;

    public string TokenHelp => "DigitalOcean Control Panel → API → Personal access tokens bölümünden oluşturun. Listeleme için 'Read', droplet oluşturmak için 'Write' kapsamı gerekir.";

    public string Currency => DigitalOceanPlugin.Currency;

    public async Task<ServiceResult<string>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(token, HttpMethod.Get, "account", null, cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<string>.Failure(response.Message!);

        using var document = response.Data!;
        if (!document.RootElement.TryGetProperty("account", out var account))
            return ServiceResult<string>.Success("DigitalOcean hesabı");

        var team = account.TryGetProperty("team", out var t) && t.ValueKind == JsonValueKind.Object ? Text(t, "name") : null;
        return ServiceResult<string>.Success(team is null ? "DigitalOcean hesabı" : $"DigitalOcean · {team}");
    }

    public async Task<ServiceResult<IReadOnlyList<CloudServerInfo>>> ListServersAsync(string token, CancellationToken cancellationToken = default)
    {
        var items = await GetAllPagesAsync(token, "droplets", "droplets", cancellationToken);
        return items.IsSuccess
            ? ServiceResult<IReadOnlyList<CloudServerInfo>>.Success(items.Data!.Select(ParseDroplet).ToList())
            : ServiceResult<IReadOnlyList<CloudServerInfo>>.Failure(items.Message!);
    }

    public async Task<ServiceResult<CloudCatalog>> GetCatalogAsync(string token, CancellationToken cancellationToken = default)
    {
        var regions = await GetAllPagesAsync(token, "regions", "regions", cancellationToken);
        if (!regions.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(regions.Message!);

        var sizes = await GetAllPagesAsync(token, "sizes", "sizes", cancellationToken);
        if (!sizes.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(sizes.Message!);

        var images = await GetAllPagesAsync(token, "images?type=distribution", "images", cancellationToken);
        if (!images.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(images.Message!);

        var regionOptions = regions.Data!
            .Where(r => IsTrue(r, "available") && Text(r, "slug") is not null)
            .Select(r => new CloudOption(Text(r, "slug")!, $"{Text(r, "name")} ({Text(r, "slug")})"))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sizeOptions = sizes.Data!
            .Where(s => IsTrue(s, "available") && Text(s, "slug") is not null)
            .Select(ParseSize)
            .Where(o => o.Regions is { Count: > 0 })
            .OrderBy(o => o.MonthlyPrice ?? decimal.MaxValue)
            .ToList();

        var imageOptions = images.Data!
            .Where(i => Text(i, "slug") is not null && (Text(i, "status") ?? "available") == "available")
            .Select(i => new CloudOption(Text(i, "slug")!, $"{Text(i, "distribution")} {Text(i, "name")}".Trim()))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return ServiceResult<CloudCatalog>.Success(new CloudCatalog(regionOptions, sizeOptions, imageOptions, Currency));
    }

    public async Task<ServiceResult<CloudCreateResult>> CreateServerAsync(string token, CloudCreateServerRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = request.Name,
            ["region"] = request.Region,
            ["size"] = request.Size,
            ["image"] = request.Image
        };
        if (!string.IsNullOrEmpty(request.UserData))
            payload["user_data"] = request.UserData;

        var response = await SendAsync(token, HttpMethod.Post, "droplets", JsonContent.Create(payload), cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<CloudCreateResult>.Failure(response.Message!);

        using var document = response.Data!;
        return document.RootElement.TryGetProperty("droplet", out var droplet)
            ? ServiceResult<CloudCreateResult>.Success(new CloudCreateResult(ParseDroplet(droplet), null))
            : ServiceResult<CloudCreateResult>.Failure("DigitalOcean beklenmeyen bir yanıt döndürdü.");
    }

    internal static CloudServerInfo ParseDroplet(JsonElement droplet)
    {
        string? ip = null;
        if (droplet.TryGetProperty("networks", out var networks) && networks.ValueKind == JsonValueKind.Object
            && networks.TryGetProperty("v4", out var v4) && v4.ValueKind == JsonValueKind.Array)
            ip = v4.EnumerateArray().Where(n => Text(n, "type") == "public").Select(n => Text(n, "ip_address")).FirstOrDefault(a => a is not null);

        string? region = null;
        if (droplet.TryGetProperty("region", out var r) && r.ValueKind == JsonValueKind.Object)
            region = Text(r, "slug");

        decimal? price = null;
        if (droplet.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Object
            && size.TryGetProperty("price_monthly", out var monthly) && monthly.ValueKind == JsonValueKind.Number)
            price = decimal.Round(monthly.GetDecimal(), 2);

        return new CloudServerInfo(
            droplet.GetProperty("id").ToString(),
            Text(droplet, "name") ?? string.Empty,
            Text(droplet, "status") ?? "unknown",
            ip,
            region,
            Text(droplet, "size_slug"),
            price,
            price is null ? null : DigitalOceanPlugin.Currency,
            DateTime.TryParse(Text(droplet, "created_at"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created) ? created : null);
    }

    private static CloudOption ParseSize(JsonElement size)
    {
        var slug = Text(size, "slug")!;
        var vcpus = size.TryGetProperty("vcpus", out var v) ? v.ToString() : "?";
        var memoryMb = size.TryGetProperty("memory", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : 0;
        var disk = size.TryGetProperty("disk", out var d) ? d.ToString() : "?";
        var memory = memoryMb >= 1024 ? $"{memoryMb / 1024d:0.#} GB" : $"{memoryMb} MB";
        decimal? price = size.TryGetProperty("price_monthly", out var p) && p.ValueKind == JsonValueKind.Number ? decimal.Round(p.GetDecimal(), 2) : null;
        var regions = size.TryGetProperty("regions", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
        return new CloudOption(slug, $"{slug} · {vcpus} vCPU · {memory} RAM · {disk} GB disk", Text(size, "description"), price, regions);
    }

    private static bool IsTrue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

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
            var response = await SendAsync(token, HttpMethod.Get, $"{path}{separator}page={page}&per_page={PageSize}", null, cancellationToken);
            if (!response.IsSuccess)
                return ServiceResult<List<JsonElement>>.Failure(response.Message!);

            using var document = response.Data!;
            var count = 0;
            if (document.RootElement.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    items.Add(item.Clone());
                    count++;
                }
            }

            var hasNext = document.RootElement.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Object
                && links.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Object
                && pages.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String;
            if (!hasNext || count == 0)
                break;
        }

        return ServiceResult<List<JsonElement>>.Success(items);
    }

    private async Task<ServiceResult<JsonDocument>> SendAsync(string token, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(DigitalOceanPlugin.HttpClientName);
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
            return ServiceResult<JsonDocument>.Failure("DigitalOcean API zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return ServiceResult<JsonDocument>.Failure("DigitalOcean API'ye bağlanılamadı.");
        }
        catch (JsonException)
        {
            return ServiceResult<JsonDocument>.Failure("DigitalOcean beklenmeyen bir yanıt döndürdü.");
        }
    }

    internal static string ErrorMessage(HttpStatusCode status, string body) =>
        CloudApiErrorMessage.Create(status, body, "DigitalOcean",
            "API anahtarının bu işlem için yetkisi yok (droplet oluşturmak için 'Write' kapsamı gerekir).",
            root => Text(root, "message"));
}
