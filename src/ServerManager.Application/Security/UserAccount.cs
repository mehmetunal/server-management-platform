namespace ServerManager.Application.Security;

public sealed record UserAccount(string Name, int Uid, string Shell)
{
    public bool CanLogin => !Shell.EndsWith("nologin", StringComparison.Ordinal) && !Shell.EndsWith("/false", StringComparison.Ordinal) && Shell.Length > 0;
}
