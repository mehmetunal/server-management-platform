using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Dokploy;

/// <summary>SSH ile görülen sunucu durumu ile panelden yapılan HTTP kontrolünü tek bir duruma indirger.</summary>
public static class DokployStatusEvaluator
{
    private const string TraefikContainer = "dokploy-traefik";

    public static (DokployStatus Status, string Message) Evaluate(DokployHostStatusDto? host, string? hostError, DokployHttpProbeResult? probe)
    {
        if (host is null)
        {
            return probe?.IsSuccess == true
                ? (DokployStatus.Running, $"Dokploy panelden erişilebilir; sunucuya SSH ile bağlanılamadı: {hostError}")
                : (DokployStatus.Unreachable, $"Sunucuya bağlanılamadı: {hostError ?? "bilinmeyen hata"}");
        }

        if (!host.DockerAvailable)
        {
            return probe?.IsSuccess == true
                ? (DokployStatus.Running, $"Dokploy panelden erişilebilir; Docker durumu okunamadı: {host.DockerError}")
                : (DokployStatus.Unknown, $"Docker durumu okunamadı: {host.DockerError ?? "bilinmeyen hata"}");
        }

        if (!host.IsInstalled)
            return (DokployStatus.NotInstalled, "Sunucuda Dokploy servisi bulunamadı.");

        var dokploy = host.Services.First(s => s.Name == "dokploy");
        if (!dokploy.IsRunning)
            return (DokployStatus.Stopped, $"Dokploy servisi çalışmıyor ({dokploy.RunningReplicas}/{dokploy.DesiredReplicas}).");

        if (!host.LocalHealthy)
            return (DokployStatus.Degraded, "Dokploy servisi çalışıyor ancak /api/health yanıt vermiyor.");

        var stopped = host.Services.Where(s => s.Name != "dokploy" && !s.IsRunning).Select(s => s.Name).ToList();
        if (host.Services.All(s => s.Name != TraefikContainer))
        {
            var traefik = host.Containers.FirstOrDefault(c => c.Name == TraefikContainer);
            if (traefik is null || !traefik.IsRunning)
                stopped.Add(TraefikContainer);
        }

        if (stopped.Count > 0)
            return (DokployStatus.Degraded, $"Çalışmayan bileşen: {string.Join(", ", stopped)}.");

        if (probe is { IsSuccess: false })
            return (DokployStatus.Degraded, $"Sunucu içinde sağlıklı ancak panelden adrese erişilemiyor: {probe.Message}");

        return (DokployStatus.Running, "Dokploy çalışıyor.");
    }
}
