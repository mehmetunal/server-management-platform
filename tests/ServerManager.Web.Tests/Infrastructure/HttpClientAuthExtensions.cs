using System.Net;
using System.Text.RegularExpressions;

namespace ServerManager.Web.Tests.Infrastructure;

public static partial class HttpClientAuthExtensions
{
    public const string AntiforgeryHeader = "RequestVerificationToken";

    /// <summary>Sayfanın layout'unda üretilen antiforgery token'ını okur (cookie istemcide saklanır).</summary>
    public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = AntiforgeryInput().Match(html);
        Assert.True(match.Success, $"{path} sayfasında antiforgery token'ı bulunamadı.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    /// <summary>Giriş formunu tarayıcıdaki gibi (AJAX + antiforgery başlığı) gönderir.</summary>
    public static async Task<HttpResponseMessage> PostLoginAsync(
        this HttpClient client, string email, string password, CancellationToken cancellationToken, string? antiforgeryToken = null)
    {
        antiforgeryToken ??= await client.GetAntiforgeryTokenAsync("/Account/Login", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password
            })
        };
        request.Headers.Add(AntiforgeryHeader, antiforgeryToken);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await client.SendAsync(request, cancellationToken);
    }

    public static async Task LoginAsync(this HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        using var response = await client.PostLoginAsync(email, password, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Giriş başarısız ({(int)response.StatusCode}): {body}");
    }

    public static HttpRequestMessage JsonGet(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return request;
    }

    [GeneratedRegex("""<input(?=[^>]*name="__RequestVerificationToken")[^>]*value="(?<token>[^"]+)"[^>]*>""")]
    private static partial Regex AntiforgeryInput();
}
