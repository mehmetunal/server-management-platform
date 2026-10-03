namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed record DokployHttpProbeResult(bool IsSuccess, int? ResponseTimeMs, string Message);
