namespace ServerManager.Application.Security;

public sealed class UserFacts
{
    /// <summary>root, UID 0 ve UID ≥ 1000 olan (nobody hariç) hesaplar.</summary>
    public IReadOnlyList<UserAccount> Accounts { get; init; } = [];

    /// <summary>sudo, wheel ve admin gruplarının üyeleri.</summary>
    public IReadOnlyList<string> SudoMembers { get; init; } = [];

    public bool ShadowReadable { get; init; }

    public IReadOnlyList<string> EmptyPasswordUsers { get; init; } = [];

    public IReadOnlyList<string> NoPasswordSudoRules { get; init; } = [];
}
