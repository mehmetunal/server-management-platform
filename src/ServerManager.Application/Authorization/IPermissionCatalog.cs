namespace ServerManager.Application.Authorization;

/// <summary>Panelde gösterilen izin: kod, Türkçe açıklama ve modül (eklenti izinlerinde eklentinin adı).</summary>
public sealed record PermissionInfo(string Name, string DisplayName, string Group, bool IsPlugin);

/// <summary>Çekirdek ve eklentilerin tanımladığı tüm izinler ile yerleşik rollerin varsayılan izinleri.</summary>
public interface IPermissionCatalog
{
    /// <summary>Çekirdek izinler önce, eklenti izinleri sonra; her izin bir kez.</summary>
    IReadOnlyList<PermissionInfo> All { get; }

    bool IsKnown(string permission);

    /// <summary>Yerleşik rolün varsayılan izinleri (çekirdek matris + eklentilerin önerdikleri). SuperAdmin için tüm izinler.</summary>
    IReadOnlyList<string> DefaultsFor(string roleName);
}
