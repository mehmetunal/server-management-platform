namespace ServerManager.Application.Validators.Account;

public static class TwoFactorCodes
{
    public const int RecoveryCodeCount = 10;

    /// <summary>Uygulamalar kodu "123 456" gibi gösterebildiği için boşluk ve tire yok sayılır.</summary>
    public static string Normalize(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();

    public static bool IsAuthenticatorCode(string? code)
    {
        var normalized = Normalize(code);
        return normalized.Length == 6 && normalized.All(char.IsAsciiDigit);
    }
}
