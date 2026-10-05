namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

/// <param name="Version">Boşsa betik son kararlı sürümü kurar.</param>
/// <param name="Elevate">Kullanıcı root değilse betik sudo ile çalıştırılır.</param>
/// <param name="ExpectedSha256">Doluysa betik yalnızca özeti bununla eşleşirse çalıştırılır.</param>
public sealed record DokployInstallPlan(string ScriptUrl, string? Version, bool Elevate, bool UseBash, TimeSpan Timeout, string? ExpectedSha256 = null);
