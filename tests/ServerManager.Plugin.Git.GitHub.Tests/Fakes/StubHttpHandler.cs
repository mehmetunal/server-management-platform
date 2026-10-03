using System.Net;
using System.Text;

namespace ServerManager.Plugin.Git.GitHub.Tests.Fakes;

/// <summary>İstekleri kaydeder ve yol + sorguya göre hazır yanıt döner; eşleşmeyen istek 404 alır.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new(StringComparer.Ordinal);

    public List<RecordedRequest> Requests { get; } = [];

    public StubHttpHandler Respond(string method, string pathAndQuery, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses[$"{method} {pathAndQuery}"] = (status, body);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method.Method,
            request.RequestUri!.PathAndQuery,
            request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues("X-GitHub-Api-Version", out var versions) ? versions.FirstOrDefault() : null,
            request.Headers.UserAgent.ToString(),
            body));

        return _responses.TryGetValue($"{request.Method.Method} {request.RequestUri.PathAndQuery}", out var response)
            ? new HttpResponseMessage(response.Status) { Content = new StringContent(response.Body, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"message":"Not Found"}""") };
    }
}
