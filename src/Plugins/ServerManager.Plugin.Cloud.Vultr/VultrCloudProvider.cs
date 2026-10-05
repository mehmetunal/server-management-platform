using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServerManager.Application.Cloud;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.Interfaces.Cloud;

namespace ServerManager.Plugin.Cloud.Vultr;

public sealed class VultrCloudProvider : ICloudProvider
{
    private const int PageSize = 100;
    private const int MaxPages = 20;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<VultrOptions> _options;

    public VultrCloudProvider(IHttpClientFactory httpClientFactory, IOptions<VultrOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string SystemName => VultrPlugin.SystemName;

    public string DisplayName => VultrPlugin.DisplayName;

    public string TokenHelp => "Vultr müşteri paneli → Account → API → Personal Access Token bölümünden oluşturun. Listeleme ve sunucu oluşturma aynı anahtarla yapılır.";

    public string Currency => VultrPlugin.Currency;

    public async Task<ServiceResult<string>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync(token, "account", cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<string>.Failure(response.Message!);

        using var document = response.Data!;
        var name = document.RootElement.TryGetProperty("account", out var account) ? Text(account, "name") : null;
        return ServiceResult<string>.Success(string.IsNullOrWhiteSpace(name) ? "Vultr hesabı" : $"Vultr · {name}");
    }

    public async Task<ServiceResult<IReadOnlyList<CloudServerInfo>>> ListServersAsync(string token, CancellationToken cancellationToken = default)
    {
        var instances = await GetCursorPagesAsync(token, "instances", "instances", cancellationToken);
        if (!instances.IsSuccess)
            return ServiceResult<IReadOnlyList<CloudServerInfo>>.Failure(instances.Message!);

        var plans = await GetCursorPagesAsync(token, "plans", "plans", cancellationToken);
        var prices = plans.IsSuccess
            ? plans.Data!.Select(p => (Id: Text(p, "id"), Price: Money(p, "monthly_cost"))).Where(p => p.Id is not null).ToDictionary(p => p.Id!, p => p.Price)
            : new Dictionary<string, decimal?>();

        return ServiceResult<IReadOnlyList<CloudServerInfo>>.Success(instances.Data!.Select(i => ParseInstance(i, prices)).ToList());
    }

    public async Task<ServiceResult<CloudCatalog>> GetCatalogAsync(string token, CancellationToken cancellationToken = default)
    {
        var regions = await GetCursorPagesAsync(token, "regions", "regions", cancellationToken);
        if (!regions.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(regions.Message!);

        var plans = await GetCursorPagesAsync(token, "plans", "plans", cancellationToken);
        if (!plans.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(plans.Message!);

        var systems = await GetCursorPagesAsync(token, "os", "os", cancellationToken);
        if (!systems.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(systems.Message!);

        var regionOptions = regions.Data!
            .Where(r => Text(r, "id") is not null)
            .Select(r => new CloudOption(Text(r, "id")!, $"{Text(r, "city") ?? Text(r, "id")} ({Text(r, "id")})", Text(r, "country")))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sizes = plans.Data!
            .Where(p => Text(p, "id") is not null)
            .Select(ParsePlan)
            .Where(o => o.Regions is { Count: > 0 })
            .OrderBy(o => o.MonthlyPrice ?? decimal.MaxValue)
            .ToList();

        var images = systems.Data!
            .Select(o => (Id: Number(o, "id"), Name: Text(o, "name"), Arch: Text(o, "arch")))
            .Where(o => o.Id is not null && o.Name is not null)
            .Select(o => new CloudOption(o.Id!, o.Name!, o.Arch))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return ServiceResult<CloudCatalog>.Success(new CloudCatalog(regionOptions, sizes, images, Currency));
    }

    public async Task<ServiceResult<CloudCreateResult>> CreateServerAsync(string token, CloudCreateServerRequest request, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(request.Image, NumberStyles.Integer, CultureInfo.InvariantCulture, out var osId))
            return ServiceResult<CloudCreateResult>.Failure("Vultr imaj kimliği sayı olmalıdır.");

        var payload = new Dictionary<string, object?>
        {
            ["region"] = request.Region,
            ["plan"] = request.Size,
            ["os_id"] = osId,
            ["label"] = request.Name,
            ["hostname"] = request.Name
        };
        if (!string.IsNullOrEmpty(request.UserData))
            payload["user_data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.UserData));

        var response = await SendAsync(token, HttpMethod.Post, "instances", JsonContent.Create(payload), cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<CloudCreateResult>.Failure(response.Message!);

        using var document = response.Data!;
        if (!document.RootElement.TryGetProperty("instance", out var instance))
            return ServiceResult<CloudCreateResult>.Failure("Vultr beklenmeyen bir yanıt döndürdü.");

        return ServiceResult<CloudCreateResult>.Success(new CloudCreateResult(ParseInstance(instance, new Dictionary<string, decimal?>()), Text(instance, "default_password")));
    }

    internal static CloudServerInfo ParseInstance(JsonElement instance, IReadOnlyDictionary<string, decimal?> prices)
    {
        var plan = Text(instance, "plan");
        prices.TryGetValue(plan ?? string.Empty, out var price);
        var ip = Text(instance, "main_ip");
        if (ip is "0.0.0.0")
            ip = null;

        return new CloudServerInfo(
            Text(instance, "id") ?? string.Empty,
            Text(instance, "label") ?? Text(instance, "hostname") ?? string.Empty,
            Text(instance, "status") ?? "unknown",
            ip,
            Text(instance, "region"),
            plan,
            price,
            price is null ? null : VultrPlugin.Currency,
            DateTime.TryParse(Text(instance, "date_created"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created) ? created : null);
    }

    private static CloudOption ParsePlan(JsonElement plan)
    {
        var id = Text(plan, "id")!;
        var cores = Number(plan, "vcpu_count") ?? "?";
        var ram = plan.TryGetProperty("ram", out var ramValue) && ramValue.TryGetInt32(out var megabytes) ? (megabytes / 1024d).ToString("0.#", CultureInfo.InvariantCulture) : "?";
        var disk = Number(plan, "disk") ?? "?";
        var regions = plan.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array
            ? locations.EnumerateArray().Select(l => l.GetString()).Where(l => l is not null).Select(l => l!).ToList()
            : [];
        return new CloudOption(id, $"{id} · {cores} vCPU · {ram} GB RAM · {disk} GB disk", Text(plan, "type"), Money(plan, "monthly_cost"), regions);
    }

    private async Task<ServiceResult<List<JsonElement>>> GetCursorPagesAsync(string token, string resource, string property, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        string? cursor = null;
        for (var page = 1; page <= MaxPages; page++)
        {
            var path = cursor is null
                ? $"{resource}?per_page={PageSize}"
                : $"{resource}?per_page={PageSize}&cursor={Uri.EscapeDataString(cursor)}";
            var response = await GetAsync(token, path, cancellationToken);
            if (!response.IsSuccess)
                return ServiceResult<List<JsonElement>>.Failure(response.Message!);

            using var document = response.Data!;
            if (document.RootElement.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
                items.AddRange(array.EnumerateArray().Select(e => e.Clone()));

            cursor = document.RootElement.TryGetProperty("meta", out var meta)
                && meta.TryGetProperty("links", out var links)
                && links.TryGetProperty("next", out var next)
                && next.ValueKind == JsonValueKind.String
                    ? next.GetString()
                    : null;
            if (string.IsNullOrEmpty(cursor))
                break;
        }

        return ServiceResult<List<JsonElement>>.Success(items);
    }

    private Task<ServiceResult<JsonDocument>> GetAsync(string token, string path, CancellationToken cancellationToken) =>
        SendAsync(token, HttpMethod.Get, path, null, cancellationToken);

    private async Task<ServiceResult<JsonDocument>> SendAsync(string token, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(VultrPlugin.HttpClientName);
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
            return ServiceResult<JsonDocument>.Failure("Vultr API zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return ServiceResult<JsonDocument>.Failure("Vultr API'ye bağlanılamadı.");
        }
        catch (JsonException)
        {
            return ServiceResult<JsonDocument>.Failure("Vultr beklenmeyen bir yanıt döndürdü.");
        }
    }

    internal static string ErrorMessage(HttpStatusCode status, string body) =>
        CloudApiErrorMessage.Create(status, body, "Vultr",
            "API anahtarının bu işlem için yetkisi yok.",
            root =>
            {
                var error = root.TryGetProperty("error", out var value) ? value : default;
                return error.ValueKind == JsonValueKind.String ? error.GetString() : Text(error, "message");
            });

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.ToString()
            : null;

    private static decimal? Money(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetDecimal(out var amount) ? decimal.Round(amount, 2) : null;
}
