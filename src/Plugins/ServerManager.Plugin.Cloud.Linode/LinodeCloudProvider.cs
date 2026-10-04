using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.Interfaces.Cloud;

namespace ServerManager.Plugin.Cloud.Linode;

public sealed partial class LinodeCloudProvider : ICloudProvider
{
    private const int PageSize = 100;
    private const int MaxPages = 20;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<LinodeOptions> _options;

    public LinodeCloudProvider(IHttpClientFactory httpClientFactory, IOptions<LinodeOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string SystemName => LinodePlugin.SystemName;

    public string DisplayName => LinodePlugin.DisplayName;

    public string TokenHelp => "Linode Cloud Manager → API Tokens bölümünden kişisel erişim anahtarı oluşturun. Listeleme için 'linodes:read_only', sunucu oluşturmak için 'linodes:read_write' kapsamı gerekir.";

    public string Currency => LinodePlugin.Currency;

    public async Task<ServiceResult<string>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync(token, $"linode/instances?page=1&page_size=1", cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<string>.Failure(response.Message!);

        using var document = response.Data!;
        var total = document.RootElement.TryGetProperty("results", out var results) && results.TryGetInt32(out var count) ? count : (int?)null;
        return ServiceResult<string>.Success(total is null ? "Linode hesabı" : $"Linode hesabı · {total} sunucu");
    }

    public async Task<ServiceResult<IReadOnlyList<CloudServerInfo>>> ListServersAsync(string token, CancellationToken cancellationToken = default)
    {
        var instances = await GetPagesAsync(token, "linode/instances", cancellationToken);
        if (!instances.IsSuccess)
            return ServiceResult<IReadOnlyList<CloudServerInfo>>.Failure(instances.Message!);

        var types = await GetPagesAsync(token, "linode/types", cancellationToken);
        var prices = types.IsSuccess
            ? types.Data!.Select(t => (Id: Text(t, "id"), Price: Monthly(t))).Where(t => t.Id is not null).ToDictionary(t => t.Id!, t => t.Price)
            : new Dictionary<string, decimal?>();

        return ServiceResult<IReadOnlyList<CloudServerInfo>>.Success(instances.Data!.Select(i => ParseInstance(i, prices)).ToList());
    }

    public async Task<ServiceResult<CloudCatalog>> GetCatalogAsync(string token, CancellationToken cancellationToken = default)
    {
        var regions = await GetPagesAsync(token, "regions", cancellationToken);
        if (!regions.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(regions.Message!);

        var types = await GetPagesAsync(token, "linode/types", cancellationToken);
        if (!types.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(types.Message!);

        var images = await GetPagesAsync(token, "images", cancellationToken);
        if (!images.IsSuccess)
            return ServiceResult<CloudCatalog>.Failure(images.Message!);

        var regionOptions = regions.Data!
            .Where(r => Text(r, "id") is not null && Text(r, "status") is "ok" && HasCapability(r, "Linodes"))
            .Select(r => new CloudOption(Text(r, "id")!, Text(r, "label") ?? Text(r, "id")!))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sizes = types.Data!
            .Where(t => Text(t, "id") is not null)
            .Select(ParseType)
            .OrderBy(o => o.MonthlyPrice ?? decimal.MaxValue)
            .ToList();

        var imageOptions = images.Data!
            .Where(i => Text(i, "id") is { } id && id.StartsWith("linode/", StringComparison.Ordinal) && Text(i, "status") is "available")
            .Select(i => new CloudOption(Text(i, "id")!, Text(i, "label") ?? Text(i, "id")!, Text(i, "description")))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return ServiceResult<CloudCatalog>.Success(new CloudCatalog(regionOptions, sizes, imageOptions, Currency));
    }

    public async Task<ServiceResult<CloudCreateResult>> CreateServerAsync(string token, CloudCreateServerRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Name.Length is < 3 or > 64 || !LabelPattern().IsMatch(request.Name))
            return ServiceResult<CloudCreateResult>.Failure("Linode adı harfle başlamalı, 3–64 karakter olmalı ve nokta, art arda tire veya alt çizgi içermemelidir.");

        var password = NewRootPassword();
        var payload = new Dictionary<string, object?>
        {
            ["label"] = request.Name,
            ["region"] = request.Region,
            ["type"] = request.Size,
            ["image"] = request.Image,
            ["root_pass"] = password,
            ["booted"] = true
        };
        if (!string.IsNullOrEmpty(request.UserData))
            payload["metadata"] = new Dictionary<string, string> { ["user_data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.UserData)) };

        var response = await SendAsync(token, HttpMethod.Post, "linode/instances", JsonContent.Create(payload), cancellationToken);
        if (!response.IsSuccess)
            return ServiceResult<CloudCreateResult>.Failure(response.Message!);

        using var document = response.Data!;
        return ServiceResult<CloudCreateResult>.Success(new CloudCreateResult(ParseInstance(document.RootElement, new Dictionary<string, decimal?>()), password));
    }

    internal static CloudServerInfo ParseInstance(JsonElement instance, IReadOnlyDictionary<string, decimal?> prices)
    {
        var type = Text(instance, "type");
        prices.TryGetValue(type ?? string.Empty, out var price);
        return new CloudServerInfo(
            instance.TryGetProperty("id", out var id) ? id.ToString() : string.Empty,
            Text(instance, "label") ?? string.Empty,
            Text(instance, "status") ?? "unknown",
            PublicIp(instance),
            Text(instance, "region"),
            type,
            price,
            price is null ? null : LinodePlugin.Currency,
            DateTime.TryParse(Text(instance, "created"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created) ? created : null);
    }

    private static CloudOption ParseType(JsonElement type)
    {
        var id = Text(type, "id")!;
        var cores = Number(type, "vcpus") ?? "?";
        var memory = type.TryGetProperty("memory", out var memoryValue) && memoryValue.TryGetInt32(out var megabytes)
            ? (megabytes / 1024d).ToString("0.#", CultureInfo.InvariantCulture)
            : "?";
        var disk = type.TryGetProperty("disk", out var diskValue) && diskValue.TryGetInt32(out var megabytesDisk)
            ? (megabytesDisk / 1024d).ToString("0.#", CultureInfo.InvariantCulture)
            : "?";
        return new CloudOption(id, $"{Text(type, "label") ?? id} · {cores} vCPU · {memory} GB RAM · {disk} GB disk", null, Monthly(type));
    }

    private static decimal? Monthly(JsonElement type) =>
        type.TryGetProperty("price", out var price) && price.ValueKind == JsonValueKind.Object && price.TryGetProperty("monthly", out var monthly) && monthly.TryGetDecimal(out var amount)
            ? decimal.Round(amount, 2)
            : null;

    private static string? PublicIp(JsonElement instance)
    {
        if (!instance.TryGetProperty("ipv4", out var list) || list.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in list.EnumerateArray())
        {
            var ip = item.GetString();
            if (ip is null || IsPrivate(ip))
                continue;
            return ip;
        }

        return null;
    }

    private static bool IsPrivate(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31);
    }

    private static bool HasCapability(JsonElement element, string capability) =>
        element.TryGetProperty("capabilities", out var list) && list.ValueKind == JsonValueKind.Array
        && list.EnumerateArray().Any(c => c.ValueKind == JsonValueKind.String && c.GetString() == capability);

    private static string NewRootPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(20);
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return "Aa1!" + new string(chars);
    }

    private async Task<ServiceResult<List<JsonElement>>> GetPagesAsync(string token, string resource, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var separator = resource.Contains('?') ? '&' : '?';
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await GetAsync(token, $"{resource}{separator}page={page}&page_size={PageSize}", cancellationToken);
            if (!response.IsSuccess)
                return ServiceResult<List<JsonElement>>.Failure(response.Message!);

            using var document = response.Data!;
            if (document.RootElement.TryGetProperty("data", out var array) && array.ValueKind == JsonValueKind.Array)
                items.AddRange(array.EnumerateArray().Select(e => e.Clone()));

            var pages = document.RootElement.TryGetProperty("pages", out var pageCount) && pageCount.TryGetInt32(out var total) ? total : 1;
            if (page >= pages)
                break;
        }

        return ServiceResult<List<JsonElement>>.Success(items);
    }

    private Task<ServiceResult<JsonDocument>> GetAsync(string token, string path, CancellationToken cancellationToken) =>
        SendAsync(token, HttpMethod.Get, path, null, cancellationToken);

    private async Task<ServiceResult<JsonDocument>> SendAsync(string token, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(LinodePlugin.HttpClientName);
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
            return ServiceResult<JsonDocument>.Failure("Linode API zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return ServiceResult<JsonDocument>.Failure("Linode API'ye bağlanılamadı.");
        }
        catch (JsonException)
        {
            return ServiceResult<JsonDocument>.Failure("Linode beklenmeyen bir yanıt döndürdü.");
        }
    }

    internal static string ErrorMessage(HttpStatusCode status, string body)
    {
        string? detail = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                detail = string.Join(" ", errors.EnumerateArray().Select(e => Text(e, "reason")).Where(r => r is not null));
        }
        catch (JsonException)
        {
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => "API anahtarı geçersiz veya iptal edilmiş.",
            HttpStatusCode.Forbidden => "API anahtarının bu işlem için yetkisi yok (sunucu oluşturmak için 'linodes:read_write' gerekir).",
            HttpStatusCode.TooManyRequests => "Linode istek sınırına ulaşıldı; biraz sonra tekrar deneyin.",
            _ => string.IsNullOrWhiteSpace(detail) ? $"Linode isteği başarısız oldu (HTTP {(int)status})." : $"Linode: {detail}"
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.ToString()
            : null;

    [GeneratedRegex("^[a-zA-Z]((?!--|__)[a-zA-Z0-9_-])+$")]
    private static partial Regex LabelPattern();
}
