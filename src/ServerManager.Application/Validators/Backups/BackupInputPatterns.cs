namespace ServerManager.Application.Validators.Backups;

/// <summary>Betiklere tırnaklanarak giren değerler yine de dar bir karakter kümesiyle sınırlandırılır.</summary>
public static class BackupInputPatterns
{
    public const string DockerName = "^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,254}$";
    public const string DatabaseName = "^[A-Za-z0-9_$][A-Za-z0-9_.$-]{0,127}$";
    public const string DatabaseUser = "^[A-Za-z0-9_][A-Za-z0-9_.@-]{0,127}$";
    public const string Host = "^[A-Za-z0-9_\\[][A-Za-z0-9_.:\\[\\]-]{0,254}$";
}
