namespace ServerManager.Application.Security;

public sealed class FirewallFacts
{
    public IReadOnlyList<FirewallTool> Tools { get; init; } = [];

    public bool AnyActive => Tools.Any(t => t.Active == true);

    /// <summary>Kurulu araçlardan en az birinin durumu okunabildiyse true.</summary>
    public bool StateKnown => Tools.Any(t => t.Active.HasValue);
}
