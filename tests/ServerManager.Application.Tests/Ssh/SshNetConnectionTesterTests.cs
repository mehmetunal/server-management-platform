using System.Security.Cryptography;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Application.Tests.Ssh;

public class SshNetConnectionTesterTests
{
    [Fact]
    public void Fingerprint_uses_openssh_sha256_format()
    {
        var hostKey = new byte[] { 1, 2, 3, 4, 5 };
        var expected = "SHA256:" + Convert.ToBase64String(SHA256.HashData(hostKey)).TrimEnd('=');

        var fingerprint = HostKeyVerifier.ComputeSha256Fingerprint(hostKey);

        Assert.Equal(expected, fingerprint);
        Assert.DoesNotContain("=", fingerprint);
    }

    [Fact]
    public void Parses_pretty_name_from_os_release()
    {
        const string output = "NAME=\"Ubuntu\"\nVERSION_ID=\"24.04\"\nPRETTY_NAME=\"Ubuntu 24.04.1 LTS\"\nID=ubuntu\n";

        Assert.Equal("Ubuntu 24.04.1 LTS", SshNetConnectionTester.ParseOperatingSystem(output));
    }

    [Fact]
    public void Falls_back_to_first_line_for_uname_output()
    {
        Assert.Equal("Linux 6.8.0-45-generic", SshNetConnectionTester.ParseOperatingSystem("Linux 6.8.0-45-generic\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n ")]
    public void Returns_null_for_empty_output(string? output)
    {
        Assert.Null(SshNetConnectionTester.ParseOperatingSystem(output));
    }
}
