using System.Net;
using System.Text;

namespace ServerManager.Plugin.Notifications.Tests.Fakes;

/// <summary>İstekleri kaydeder ve her isteğe aynı hazır yanıtı döner; ağa çıkılmaz.</summary>
public sealed class RecordingHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public RecordingHttpHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "{\"ok\":true}")
    {
        _status = status;
        _body = body;
    }

    public List<(Uri Uri, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!, body));
        return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
    }
}
