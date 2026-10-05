namespace ServerManager.Web.Middleware;

/// <summary>Konteyner/yük dengeleyici sağlık kontrolleri. Anonimdir ve yalnızca durum metnini döner (Healthy/Unhealthy).</summary>
public static class HealthEndpoints
{
    /// <summary>Süreç ayakta mı; veritabanına gitmez.</summary>
    public const string LivenessPath = "/health";

    /// <summary>Veritabanı bağlantısı dahil trafik almaya hazır mı.</summary>
    public const string ReadinessPath = "/health/ready";
}
