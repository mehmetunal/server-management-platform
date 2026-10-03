namespace ServerManager.Application.Auditing;

/// <summary>Çekirdek ve eklenti audit aksiyonlarının adları. Devre dışı eklentilerin geçmiş kayıtları da adıyla gösterilir.</summary>
public sealed class AuditActionCatalog
{
    private readonly IReadOnlyDictionary<string, string> _displayNames;

    public AuditActionCatalog(IEnumerable<IAuditActionProvider> providers)
    {
        var names = new Dictionary<string, string>(AuditActions.DisplayNames, StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            foreach (var (action, displayName) in provider.GetDisplayNames())
                names.TryAdd(action, displayName);
        }

        _displayNames = names;
    }

    public IReadOnlyDictionary<string, string> DisplayNames => _displayNames;

    public string DisplayName(string action) =>
        _displayNames.TryGetValue(action, out var text) ? text : action;
}
