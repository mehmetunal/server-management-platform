namespace ServerManager.Application.Auditing;

/// <summary>Eklentinin audit log'a yazdığı aksiyonların Türkçe adları.</summary>
public interface IAuditActionProvider
{
    IReadOnlyDictionary<string, string> GetDisplayNames();
}
