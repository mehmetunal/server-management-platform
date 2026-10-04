using System.Net;
using System.Text;

namespace ServerManager.Plugin.Cloud.Tests.Fakes;

/// <summary>İstek yolu ve sorgusuna göre hazır yanıt döner; ağa çıkılmaz. Eşleşme yoksa 404 döner.</summary>
public sealed class ScriptedHttpHandler : HttpMessageHandler
{
    private readonly List<(string PathAndQuery, HttpStatusCode Status, string Body)> _routes = [];

    public List<(HttpMethod Method, Uri Uri, string? Authorization, string Body)> Requests { get; } = [];

    public Exception? Throw { get; set; }

    public ScriptedHttpHandler Add(string pathAndQueryPrefix, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((pathAndQueryPrefix, status, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
        if (Throw is not null)
            throw Throw;

        var path = request.RequestUri!.PathAndQuery;
        var route = _routes.LastOrDefault(r => path.StartsWith(r.PathAndQuery, StringComparison.Ordinal));
        return route.Body is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
            : new HttpResponseMessage(route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
    }
}
