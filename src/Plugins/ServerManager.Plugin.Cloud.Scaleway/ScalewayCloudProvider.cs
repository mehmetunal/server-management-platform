using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.Interfaces.Cloud;

namespace ServerManager.Plugin.Cloud.Scaleway;

public sealed class ScalewayCloudProvider : ICloudProvider
{
    /// <summary>Instance API'nin yayımladığı bölgeler. Hesabın açmadığı bölge 400/404 döner ve atlanır.</summary>
    private static readonly string[] Zones =
    [
        "fr-par-1", "fr-par-2", "fr-par-3",
        "nl-ams-1", "nl-ams-2", "nl-ams-3",
        "pl-waw-1", "pl-waw-2", "pl-waw-3",
        "it-mil-1"
    ];

    private const int PageSize = 50;
    private const int MaxPages = 20;

    /// <summary>Katalog saatlik EUR yayınlar; diğer sağlayıcılarla aynı satırda göstermek için aya çevrilir.</summary>
    private const decimal HoursPerMonth = 730m;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ScalewayOptions> _options;

    public ScalewayCloudProvider(IHttpClientFactory httpClientFactory, IOptions<ScalewayOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string SystemName => ScalewayPlugin.SystemName;

    public string DisplayName => ScalewayPlugin.DisplayName;

    public string TokenHelp => "Scaleway konsolu → IAM → API keys bölümünden bir gizli anahtar (secret key) oluşturun. Listeleme ve sunucu oluşturma aynı anahtarla yapılır.";

    public string Currency => ScalewayPlugin.Currency;

    public async Task<ServiceResult<string>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var project = await FirstProjectAsync(token, cancellationToken);
        return project.IsSuccess
            ? ServiceResult<string>.Success($"Scaleway · {project.Data}")
            : ServiceResult<string>.Failure(project.Message!);
    }

    public async Task<ServiceResult<IReadOnlyList<CloudServerInfo>>> ListServersAsync(string token, CancellationToken cancellationToken = default)
    {
        var servers = new List<CloudServerInfo>();
        var prices = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        var sawZone = false;
        foreach (var zone in Zones)
        {
            var page = await GetPagesAsync(token, $"instance/v1/zones/{zone}/servers", "servers", allowMissing: true, cancellationToken);
            if (!page.IsSuccess)
                return ServiceResult<IReadOnlyList<CloudServerInfo>>.Failure(page.Message!);
            if (page.Data is null)
                continue;

            sawZone = true;
            var products = await GetAsync(token, $"instance/v1/zones/{zone}/products/servers", cancellationToken);
            if (products.Status is HttpStatusCode.OK && products.Document is not null)
            {
                using (products.Document)
                    CollectPrices(products.Document.RootElement, prices);
            }
            else
                products.Document?.Dispose();

            servers.AddRange(page.Data.Select(s => ParseServer(s, zone, prices)));
        }

        return sawZone
            ? ServiceResult<IReadOnlyList<CloudServerInfo>>.Success(servers)
            : ServiceResult<IReadOnlyList<CloudServerInfo>>.Failure("Scaleway bölgelerinden yanıt alınamadı.");
    }

    public async Task<ServiceResult<CloudCatalog>> GetCatalogAsync(string token, CancellationToken cancellationToken = default)
    {
        var sizes = new Dictionary<string, (string Name, string? Arch, decimal? Price, HashSet<string> Zones)>(StringComparer.Ordinal);
        var images = new Dictionary<string, string?>(StringComparer.Ordinal);
        var availableZones = new List<string>();

        foreach (var zone in Zones)
        {
            var products = await GetAsync(token, $"instance/v1/zones/{zone}/products/servers", cancellationToken);
            if (products.Status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                products.Document?.Dispose();
                continue;
            }

            if (products.Status is not HttpStatusCode.OK || products.Document is null)
            {
                products.Document?.Dispose();
                return ServiceResult<CloudCatalog>.Failure(products.Error ?? "Scaleway ürün listesi alınamadı.");
            }

            availableZones.Add(zone);
            using (products.Document)
            {
                if (products.Document.RootElement.TryGetProperty("servers", out var map) && map.ValueKind == JsonValueKind.Object)
                {
                    foreach (var type in map.EnumerateObject())
                    {
                        var price = MonthlyPrice(type.Value);
                        var arch = Text(type.Value, "arch");
                        var cores = Number(type.Value, "ncpus") ?? "?";
                        var ram = RamGigabytes(type.Value);
                        var name = $"{type.Name} · {cores} vCPU · {ram} GB RAM";
                        if (sizes.TryGetValue(type.Name, out var existing))
                            existing.Zones.Add(zone);
                        else
                            sizes[type.Name] = (name, arch, price, [zone]);
                    }
                }
            }

            var imagePage = await GetPagesAsync(token, $"instance/v1/zones/{zone}/images", "images", allowMissing: true, cancellationToken);
            if (!imagePage.IsSuccess)
                return ServiceResult<CloudCatalog>.Failure(imagePage.Message!);

            foreach (var image in imagePage.Data ?? [])
            {
                if (IsTrue(image, "public") && Text(image, "name") is { } name && !images.ContainsKey(name))
                    images[name] = Text(image, "arch");
            }
        }

        if (availableZones.Count == 0)
            return ServiceResult<CloudCatalog>.Failure("Scaleway bölgelerinden yanıt alınamadı.");

        var regionOptions = availableZones.Select(z => new CloudOption(z, z)).ToList();
        var sizeOptions = sizes
            .Select(s => new CloudOption(s.Key, s.Value.Name, s.Value.Arch, s.Value.Price, s.Value.Zones.Order(StringComparer.Ordinal).ToList()))
            .OrderBy(o => o.MonthlyPrice ?? decimal.MaxValue)
            .ToList();
        var imageOptions = images
            .Select(i => new CloudOption(i.Key, i.Key, i.Value))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return ServiceResult<CloudCatalog>.Success(new CloudCatalog(regionOptions, sizeOptions, imageOptions, Currency));
    }

    public async Task<ServiceResult<CloudCreateResult>> CreateServerAsync(string token, CloudCreateServerRequest request, CancellationToken cancellationToken = default)
    {
        if (!Zones.Contains(request.Region, StringComparer.Ordinal))
            return ServiceResult<CloudCreateResult>.Failure("Seçilen Scaleway bölgesi desteklenmiyor.");

        var project = await FirstProjectIdAsync(token, cancellationToken);
        if (!project.IsSuccess)
            return ServiceResult<CloudCreateResult>.Failure(project.Message!);

        var imageId = await FindImageIdAsync(token, request.Region, request.Image, cancellationToken);
        if (!imageId.IsSuccess)
            return ServiceResult<CloudCreateResult>.Failure(imageId.Message!);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = request.Name,
            ["commercial_type"] = request.Size,
            ["image"] = imageId.Data,
            ["project"] = project.Data,
            ["dynamic_ip_required"] = true
        };
        var created = await SendAsync(token, HttpMethod.Post, $"instance/v1/zones/{request.Region}/servers", JsonContent.Create(payload), cancellationToken);
        if (created.Status is not HttpStatusCode.OK and not HttpStatusCode.Created || created.Document is null)
        {
            created.Document?.Dispose();
            return ServiceResult<CloudCreateResult>.Failure(created.Error ?? "Scaleway sunucusu oluşturulamadı.");
        }

        JsonElement server;
        using (created.Document)
        {
            if (!created.Document.RootElement.TryGetProperty("server", out server))
                return ServiceResult<CloudCreateResult>.Failure("Scaleway beklenmeyen bir yanıt döndürdü.");
            server = server.Clone();
        }

        var id = Text(server, "id");
        if (id is null)
            return ServiceResult<CloudCreateResult>.Failure("Scaleway sunucu kimliği döndürmedi.");

        if (!string.IsNullOrEmpty(request.UserData))
        {
            using var userData = new StringContent(request.UserData, Encoding.UTF8, "text/plain");
            var written = await SendAsync(token, HttpMethod.Patch, $"instance/v1/zones/{request.Region}/servers/{id}/user_data/cloud-init", userData, cancellationToken);
            written.Document?.Dispose();
            if (written.Status is not HttpStatusCode.OK and not HttpStatusCode.NoContent)
                return ServiceResult<CloudCreateResult>.Failure($"Sunucu oluşturuldu ({id}) ancak cloud-init yazılamadı. {written.Error}");
        }

        if (Text(server, "state") is "stopped" or "stopped in place")
        {
            var powered = await SendAsync(token, HttpMethod.Post, $"instance/v1/zones/{request.Region}/servers/{id}/action", JsonContent.Create(new { action = "poweron" }), cancellationToken);
            powered.Document?.Dispose();
            if (powered.Status is not HttpStatusCode.OK and not HttpStatusCode.Accepted and not HttpStatusCode.NoContent)
                return ServiceResult<CloudCreateResult>.Failure($"Sunucu oluşturuldu ({id}) ancak başlatılamadı. {powered.Error}");
        }

        return ServiceResult<CloudCreateResult>.Success(new CloudCreateResult(ParseServer(server, request.Region, new Dictionary<string, decimal?>()), null));
    }

    internal static CloudServerInfo ParseServer(JsonElement server, string zone, IReadOnlyDictionary<string, decimal?> prices)
    {
        var type = Text(server, "commercial_type");
        prices.TryGetValue(type ?? string.Empty, out var price);
        string? ip = null;
        if (server.TryGetProperty("public_ip", out var publicIp) && publicIp.ValueKind == JsonValueKind.Object)
            ip = Text(publicIp, "address");

        return new CloudServerInfo(
            Text(server, "id") ?? string.Empty,
            Text(server, "name") ?? string.Empty,
            Text(server, "state") ?? "unknown",
            ip,
            Text(server, "zone") ?? zone,
            type,
            price,
            price is null ? null : ScalewayPlugin.Currency,
            DateTime.TryParse(Text(server, "creation_date"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created) ? created : null);
    }

    private async Task<ServiceResult<string>> FirstProjectAsync(string token, CancellationToken cancellationToken)
    {
        var id = await FirstProjectIdAsync(token, cancellationToken);
        return id.IsSuccess
            ? ServiceResult<string>.Success(id.Message ?? "varsayılan proje")
            : ServiceResult<string>.Failure(id.Message!);
    }

    private async Task<ServiceResult<string>> FirstProjectIdAsync(string token, CancellationToken cancellationToken)
    {
        var response = await GetAsync(token, "account/v3/projects?page=1&page_size=50", cancellationToken);
        if (response.Status is not HttpStatusCode.OK || response.Document is null)
        {
            response.Document?.Dispose();
            return ServiceResult<string>.Failure(response.Error ?? "Scaleway projeleri okunamadı.");
        }

        using (response.Document)
        {
            if (!response.Document.RootElement.TryGetProperty("projects", out var projects) || projects.ValueKind != JsonValueKind.Array || projects.GetArrayLength() == 0)
                return ServiceResult<string>.Failure("Scaleway hesabında proje bulunamadı.");

            var chosen = projects.EnumerateArray().FirstOrDefault(p => Text(p, "name") is "default");
            if (chosen.ValueKind == JsonValueKind.Undefined)
                chosen = projects[0];
            var id = Text(chosen, "id");
            var name = Text(chosen, "name") ?? "proje";
            return id is null
                ? ServiceResult<string>.Failure("Scaleway proje kimliği okunamadı.")
                : ServiceResult<string>.Success(id, name);
        }
    }

    private async Task<ServiceResult<string>> FindImageIdAsync(string token, string zone, string name, CancellationToken cancellationToken)
    {
        var images = await GetPagesAsync(token, $"instance/v1/zones/{zone}/images", "images", allowMissing: false, cancellationToken);
        if (!images.IsSuccess || images.Data is null)
            return ServiceResult<string>.Failure(images.Message ?? "Scaleway imajları okunamadı.");

        var match = images.Data.FirstOrDefault(i => Text(i, "name") == name || Text(i, "id") == name);
        var id = match.ValueKind == JsonValueKind.Object ? Text(match, "id") : null;
        return id is null
            ? ServiceResult<string>.Failure($"Scaleway bölgesinde '{name}' imajı bulunamadı.")
            : ServiceResult<string>.Success(id);
    }

    private async Task<ServiceResult<List<JsonElement>?>> GetPagesAsync(string token, string path, string property, bool allowMissing, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var separator = path.Contains('?') ? '&' : '?';
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await GetAsync(token, $"{path}{separator}page={page}&per_page={PageSize}", cancellationToken);
            if (allowMissing && response.Status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                response.Document?.Dispose();
                return ServiceResult<List<JsonElement>?>.Success(page == 1 && items.Count == 0 ? null : items);
            }

            if (response.Status is not HttpStatusCode.OK || response.Document is null)
            {
                response.Document?.Dispose();
                return ServiceResult<List<JsonElement>?>.Failure(response.Error ?? "Scaleway isteği başarısız oldu.");
            }

            int count;
            int total;
            using (response.Document)
            {
                count = 0;
                if (response.Document.RootElement.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in array.EnumerateArray())
                    {
                        items.Add(item.Clone());
                        count++;
                    }
                }

                total = response.Document.RootElement.TryGetProperty("total_count", out var totalCount) && totalCount.TryGetInt32(out var value) ? value : items.Count;
            }

            if (count == 0 || items.Count >= total)
                break;
        }

        return ServiceResult<List<JsonElement>?>.Success(items);
    }

    private static void CollectPrices(JsonElement root, IDictionary<string, decimal?> prices)
    {
        if (!root.TryGetProperty("servers", out var map) || map.ValueKind != JsonValueKind.Object)
            return;

        foreach (var type in map.EnumerateObject())
            prices.TryAdd(type.Name, MonthlyPrice(type.Value));
    }

    private static decimal? MonthlyPrice(JsonElement type)
    {
        if (type.TryGetProperty("monthly_price", out var monthly) && monthly.TryGetDecimal(out var month))
            return decimal.Round(month, 2);
        if (type.TryGetProperty("hourly_price", out var hourly) && hourly.TryGetDecimal(out var hour))
            return decimal.Round(hour * HoursPerMonth, 2);
        return null;
    }

    private static string RamGigabytes(JsonElement type)
    {
        if (!type.TryGetProperty("ram", out var ram) || !ram.TryGetInt64(out var bytes))
            return "?";
        var gigabytes = bytes >= 1_000_000 ? bytes / 1024d / 1024d / 1024d : bytes / 1024d;
        return gigabytes.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private async Task<(HttpStatusCode Status, JsonDocument? Document, string? Error)> GetAsync(string token, string path, CancellationToken cancellationToken) =>
        await SendAsync(token, HttpMethod.Get, path, null, cancellationToken);

    private async Task<(HttpStatusCode Status, JsonDocument? Document, string? Error)> SendAsync(string token, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ScalewayPlugin.HttpClientName);
        using var request = new HttpRequestMessage(method, new Uri(new Uri(_options.Value.ApiUrl), path)) { Content = content };
        request.Headers.TryAddWithoutValidation("X-Auth-Token", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return (response.StatusCode, null, ErrorMessage(response.StatusCode, body));

            return (response.StatusCode, JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body), null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (0, null, "Scaleway API zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return (0, null, "Scaleway API'ye bağlanılamadı.");
        }
        catch (JsonException)
        {
            return (0, null, "Scaleway beklenmeyen bir yanıt döndürdü.");
        }
    }

    internal static string ErrorMessage(HttpStatusCode status, string body)
    {
        string? detail = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            detail = Text(document.RootElement, "message");
        }
        catch (JsonException)
        {
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => "API anahtarı geçersiz veya iptal edilmiş.",
            HttpStatusCode.Forbidden => "API anahtarının bu işlem için yetkisi yok.",
            HttpStatusCode.TooManyRequests => "Scaleway istek sınırına ulaşıldı; biraz sonra tekrar deneyin.",
            _ => detail is null ? $"Scaleway isteği başarısız oldu (HTTP {(int)status})." : $"Scaleway: {detail}"
        };
    }

    private static bool IsTrue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.ToString()
            : null;
}
