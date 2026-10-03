namespace ServerManager.Application.DTOs.Dokploy;

public sealed record DokployHttpProbeResult(bool IsSuccess, int? ResponseTimeMs, string Message);
