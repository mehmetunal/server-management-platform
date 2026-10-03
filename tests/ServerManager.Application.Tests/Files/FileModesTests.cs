using ServerManager.Application.Files;

namespace ServerManager.Application.Tests.Files;

public class FileModesTests
{
    [Theory]
    [InlineData(0b111_101_101, "rwxr-xr-x", "0755")]
    [InlineData(0b110_100_100, "rw-r--r--", "0644")]
    [InlineData(0b110_000_000, "rw-------", "0600")]
    [InlineData(FileModes.SetUid | 0b111_101_101, "rwsr-xr-x", "4755")]
    [InlineData(FileModes.SetGid | 0b111_101_000, "rwxr-s---", "2750")]
    [InlineData(FileModes.Sticky | 0b111_111_111, "rwxrwxrwt", "1777")]
    [InlineData(FileModes.SetUid | FileModes.Sticky | 0b110_100_100, "rwSr--r-T", "5644")]
    public void Formats_symbolic_and_octal(int mode, string symbolic, string octal)
    {
        Assert.Equal(symbolic, FileModes.ToSymbolic(mode));
        Assert.Equal(octal, FileModes.ToOctal(mode));
    }

    [Theory]
    [InlineData("755", true)]
    [InlineData("0644", true)]
    [InlineData("4755", true)]
    [InlineData("u+x", true)]
    [InlineData("u=rwx,go=rx", true)]
    [InlineData("a-w", true)]
    [InlineData("+t", true)]
    [InlineData("g+X", true)]
    [InlineData("75", false)]
    [InlineData("0888", false)]
    [InlineData("77777", false)]
    [InlineData("u+z", false)]
    [InlineData("755; rm -rf /", false)]
    [InlineData("--reference=/etc/shadow", false)]
    [InlineData("", false)]
    public void Validates_modes(string mode, bool expected)
    {
        Assert.Equal(expected, FileModes.IsValidMode(mode));
    }

    [Theory]
    [InlineData("deploy", true)]
    [InlineData("www-data", true)]
    [InlineData("_apt", true)]
    [InlineData("machine$", true)]
    [InlineData("1000", true)]
    [InlineData("1user", false)]
    [InlineData("-rf", false)]
    [InlineData("root:root", false)]
    [InlineData("a b", false)]
    [InlineData("$(id)", false)]
    public void Validates_principals(string value, bool expected)
    {
        Assert.Equal(expected, FileModes.IsValidPrincipal(value));
    }
}
