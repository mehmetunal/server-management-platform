namespace ServerManager.Infrastructure.Ssh;

public static class ShellQuote
{
    /// <summary>POSIX shell için tek tırnakla kaçışlar; içerideki tek tırnaklar '"'"' ile kapatılıp yeniden açılır.</summary>
    public static string Quote(string value) =>
        "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
}
