using System.Net;

namespace ServerManager.Plugin.DevOps.Dokploy.Integration;

internal sealed record DokployHttpResponse(HttpStatusCode StatusCode, string Body, string? Error)
{
    public static DokployHttpResponse Failed(string error) => new(0, string.Empty, error);
}
