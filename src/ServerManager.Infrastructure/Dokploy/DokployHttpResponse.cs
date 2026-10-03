using System.Net;

namespace ServerManager.Infrastructure.Dokploy;

internal sealed record DokployHttpResponse(HttpStatusCode StatusCode, string Body, string? Error)
{
    public static DokployHttpResponse Failed(string error) => new(0, string.Empty, error);
}
