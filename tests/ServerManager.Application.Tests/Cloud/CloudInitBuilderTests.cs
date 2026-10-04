using ServerManager.Application.Cloud;

namespace ServerManager.Application.Tests.Cloud;

public class CloudInitBuilderTests
{
    private const string Key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl ops@laptop";

    [Fact]
    public void Build_returns_null_without_template_and_key()
    {
        Assert.Null(CloudInitBuilder.Build(null, "  "));
    }

    [Fact]
    public void Build_returns_template_unchanged_without_key()
    {
        Assert.Equal("#cloud-config\npackages: [nginx]\n", CloudInitBuilder.Build("#cloud-config\r\npackages: [nginx]\r\n\r\n", null));
    }

    [Fact]
    public void Build_creates_cloud_config_for_key_only()
    {
        Assert.Equal($"#cloud-config\nssh_authorized_keys:\n  - {Key}\n", CloudInitBuilder.Build(null, Key));
    }

    [Fact]
    public void Build_appends_authorized_keys_to_cloud_config()
    {
        var result = CloudInitBuilder.Build("#cloud-config\npackages: [nginx]", Key);

        Assert.Equal($"#cloud-config\npackages: [nginx]\nssh_authorized_keys:\n  - {Key}\n", result);
    }

    [Fact]
    public void Build_adds_key_to_existing_authorized_keys_list()
    {
        var result = CloudInitBuilder.Build("#cloud-config\nssh_authorized_keys:\n  - ssh-rsa AAAA old\nruncmd: [uptime]", Key);

        Assert.Equal($"#cloud-config\nssh_authorized_keys:\n  - {Key}\n  - ssh-rsa AAAA old\nruncmd: [uptime]\n", result);
    }

    [Fact]
    public void Build_inserts_install_commands_after_shebang()
    {
        var result = CloudInitBuilder.Build("#!/bin/sh\napt-get update", Key)!;

        Assert.StartsWith("#!/bin/sh\ninstall -d -m 700 /root/.ssh\n", result);
        Assert.Contains($"printf '%s\\n' '{Key}' >> /root/.ssh/authorized_keys\n", result);
        Assert.EndsWith("chmod 600 /root/.ssh/authorized_keys\napt-get update\n", result);
    }

    [Theory]
    [InlineData(Key, true)]
    [InlineData("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC7", true)]
    [InlineData("ecdsa-sha2-nistp256 AAAAE2VjZHNh= host", true)]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----", false)]
    [InlineData("ssh-ed25519 AAAA bad'quote", false)]
    [InlineData("ssh-ed25519 AAAA\nrm -rf /", false)]
    [InlineData(null, false)]
    public void IsValidPublicKey_accepts_only_single_line_public_keys(string? key, bool expected)
    {
        Assert.Equal(expected, CloudInitBuilder.IsValidPublicKey(key));
    }
}
