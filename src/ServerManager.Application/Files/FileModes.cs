using System.Text.RegularExpressions;

namespace ServerManager.Application.Files;

public static partial class FileModes
{
    public const int SetUid = 0x800;
    public const int SetGid = 0x400;
    public const int Sticky = 0x200;

    public static bool IsValidOctal(string? mode) => mode is not null && OctalPattern().IsMatch(mode);

    public static bool IsValidSymbolic(string? mode) => mode is not null && SymbolicPattern().IsMatch(mode);

    public static bool IsValidMode(string? mode) => IsValidOctal(mode) || IsValidSymbolic(mode);

    /// <summary>Kullanıcı / grup adı veya sayısal kimlik.</summary>
    public static bool IsValidPrincipal(string? value) => value is not null && PrincipalPattern().IsMatch(value);

    public static string ToOctal(int mode) => Convert.ToString(mode & 0xFFF, 8).PadLeft(4, '0');

    /// <summary>ls -l biçimi: rwxr-xr-x (setuid/setgid/sticky dahil).</summary>
    public static string ToSymbolic(int mode)
    {
        Span<char> chars = stackalloc char[9];
        chars[0] = (mode & 0x100) != 0 ? 'r' : '-';
        chars[1] = (mode & 0x80) != 0 ? 'w' : '-';
        chars[2] = Exec((mode & 0x40) != 0, (mode & SetUid) != 0, 's');
        chars[3] = (mode & 0x20) != 0 ? 'r' : '-';
        chars[4] = (mode & 0x10) != 0 ? 'w' : '-';
        chars[5] = Exec((mode & 0x8) != 0, (mode & SetGid) != 0, 's');
        chars[6] = (mode & 0x4) != 0 ? 'r' : '-';
        chars[7] = (mode & 0x2) != 0 ? 'w' : '-';
        chars[8] = Exec((mode & 0x1) != 0, (mode & Sticky) != 0, 't');
        return new string(chars);
    }

    private static char Exec(bool executable, bool special, char specialChar) => (executable, special) switch
    {
        (true, true) => specialChar,
        (false, true) => char.ToUpperInvariant(specialChar),
        (true, false) => 'x',
        _ => '-'
    };

    [GeneratedRegex("^[0-7]{3,4}$")]
    private static partial Regex OctalPattern();

    [GeneratedRegex("^[ugoa]*[-+=][rwxXst]*(,[ugoa]*[-+=][rwxXst]*)*$")]
    private static partial Regex SymbolicPattern();

    [GeneratedRegex(@"^([a-zA-Z_][a-zA-Z0-9_.-]{0,31}\$?|[0-9]{1,10})$")]
    private static partial Regex PrincipalPattern();
}
