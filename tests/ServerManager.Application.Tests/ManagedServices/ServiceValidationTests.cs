using ServerManager.Application.ManagedServices;

namespace ServerManager.Application.Tests.ManagedServices;

public class ServiceValidationTests
{
    [Theory]
    [InlineData("16")]
    [InlineData("8.4")]
    [InlineData("2022-latest")]
    [InlineData("RELEASE.2025-04-22T22-12-26Z")]
    [InlineData("_internal")]
    public void Valid_tags_are_accepted(string tag) => Assert.True(ServiceValidation.IsValidTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-latest")]
    [InlineData(".hidden")]
    [InlineData("16;rm -rf /")]
    [InlineData("16 17")]
    [InlineData("a:b")]
    [InlineData("$(id)")]
    public void Invalid_tags_are_rejected(string? tag) => Assert.False(ServiceValidation.IsValidTag(tag));

    [Fact]
    public void Tag_longer_than_128_is_rejected()
    {
        Assert.True(ServiceValidation.IsValidTag(new string('a', 128)));
        Assert.False(ServiceValidation.IsValidTag(new string('a', 129)));
    }

    [Theory]
    [InlineData("app", true)]
    [InlineData("_svc.user-1", true)]
    [InlineData("1app", false)]
    [InlineData("app user", false)]
    [InlineData("app'", false)]
    [InlineData("", false)]
    public void Usernames(string value, bool expected) => Assert.Equal(expected, ServiceValidation.IsValidUsername(value));

    [Theory]
    [InlineData("appdb", true)]
    [InlineData("_db1", true)]
    [InlineData("app-db", false)]
    [InlineData("1db", false)]
    [InlineData("db;drop", false)]
    public void Database_names(string value, bool expected) => Assert.Equal(expected, ServiceValidation.IsValidDatabaseName(value));

    [Theory]
    [InlineData("admin@example.com", true)]
    [InlineData("a.b+c@sub.example.org", true)]
    [InlineData("admin", false)]
    [InlineData("admin@local", false)]
    public void Emails(string value, bool expected) => Assert.Equal(expected, ServiceValidation.IsValidEmail(value));

    [Theory]
    [InlineData("abcdefghijkl", true)]
    [InlineData("short", false)]
    [InlineData("has space inside", false)]
    [InlineData("quote'insidepw", false)]
    [InlineData("dquote\"insidepw", false)]
    [InlineData("back\\slashpass", false)]
    [InlineData("backtick`pass1", false)]
    [InlineData("Symb0ls!#%&*@^~", true)]
    public void Standard_password_policy(string password, bool expected) =>
        Assert.Equal(expected, ServiceValidation.TryValidatePassword(ServicePasswordPolicy.Standard, password, out _));

    [Theory]
    [InlineData("Passw0rd", true)]
    [InlineData("password1", false)]
    [InlineData("PASSWORD!", false)]
    [InlineData("Pass!1", false)]
    [InlineData("Abcdefg!", true)]
    public void Sql_server_password_complexity(string password, bool expected) =>
        Assert.Equal(expected, ServiceValidation.TryValidatePassword(ServicePasswordPolicy.MssqlComplex, password, out _));

    [Fact]
    public void Password_none_policy_accepts_empty()
    {
        Assert.True(ServiceValidation.TryValidatePassword(ServicePasswordPolicy.None, null, out _));
        Assert.False(ServiceValidation.TryValidatePassword(ServicePasswordPolicy.Standard, null, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(5432, false, true)]
    [InlineData(1024, false, true)]
    [InlineData(65535, false, true)]
    [InlineData(80, false, false)]
    [InlineData(80, true, true)]
    [InlineData(0, true, false)]
    [InlineData(65536, true, false)]
    [InlineData(-1, true, false)]
    public void Host_ports_are_in_range_and_not_privileged_unless_allowed(int port, bool allowPrivileged, bool expected) =>
        Assert.Equal(expected, ServiceValidation.TryValidateHostPort(port, allowPrivileged, out _));

    [Theory]
    [InlineData("/srv/data/postgres", true)]
    [InlineData("/home/ubuntu/data/pg", true)]
    [InlineData("/var/lib/sm/postgres", true)]
    [InlineData("relative/path", false)]
    [InlineData("/", false)]
    [InlineData("/srv", false)]
    [InlineData("/etc/postgres", false)]
    [InlineData("/usr/local/data", false)]
    [InlineData("/home/ubuntu", false)]
    [InlineData("/srv/../etc", false)]
    [InlineData("/srv/da ta", false)]
    [InlineData("/srv/$(id)", false)]
    [InlineData("", false)]
    public void Host_paths_follow_deploy_path_rules(string path, bool expected) =>
        Assert.Equal(expected, ServiceValidation.TryValidateHostPath(path, out _));

    [Theory]
    [InlineData("203.0.113.10", "203.0.113.10/32")]
    [InlineData("10.0.0.5/24", "10.0.0.0/24")]
    [InlineData("192.168.1.0/24", "192.168.1.0/24")]
    [InlineData("2001:db8::1", "2001:db8::1/128")]
    [InlineData("2001:db8:abcd::/48", "2001:db8:abcd::/48")]
    [InlineData("2001:db8::ffff/32", "2001:db8::/32")]
    public void Cidrs_are_normalized(string input, string expected)
    {
        Assert.True(ServiceValidation.TryNormalizeCidr(input, out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("10.0.0")]
    [InlineData("10")]
    [InlineData("256.1.1.1")]
    [InlineData("010.0.0.1")]
    [InlineData("10.0.0.1/")]
    [InlineData("10.0.0.1/24/1")]
    [InlineData("10.0.0.1/-1")]
    [InlineData("10.0.0.1/ 8")]
    [InlineData("example.com")]
    [InlineData("10.0.0.1;rm")]
    [InlineData("2001:db8::/129")]
    [InlineData("::ffff:10.0.0.1/200")]
    public void Invalid_cidrs_are_rejected(string input) =>
        Assert.False(ServiceValidation.TryNormalizeCidr(input, out _, out _));

    [Fact]
    public void Cidr_list_accepts_separators_and_removes_duplicates()
    {
        Assert.True(ServiceValidation.TryParseCidrs("203.0.113.10, 10.0.0.0/8\n203.0.113.10/32; 2001:db8::/32", out var cidrs, out _));
        Assert.Equal(["203.0.113.10/32", "10.0.0.0/8", "2001:db8::/32"], cidrs);
        Assert.True(ServiceValidation.TryParseCidrs("  ", out var empty, out _));
        Assert.Empty(empty);
    }

    [Fact]
    public void Cidr_list_has_upper_limit()
    {
        var text = string.Join('\n', Enumerable.Range(1, ServiceValidation.MaxAllowedSources + 1).Select(i => $"10.0.{i / 256}.{i % 256}"));
        Assert.False(ServiceValidation.TryParseCidrs(text, out _, out var error));
        Assert.Contains("En fazla", error);
    }

    [Fact]
    public void Networks_skip_panel_networks_and_reject_reserved_names()
    {
        Assert.True(ServiceValidation.TryParseNetworks("app_default, sm-services sm-proxy app_default", out var networks, out _));
        Assert.Equal(["app_default"], networks);
        Assert.False(ServiceValidation.TryParseNetworks("host", out _, out _));
        Assert.False(ServiceValidation.TryParseNetworks("bad;name", out _, out _));
        Assert.False(ServiceValidation.TryParseNetworks("a1,b1,c1,d1,e1,f1", out _, out _));
    }

    [Fact]
    public void Generated_passwords_are_strong_and_url_safe()
    {
        for (var i = 0; i < 50; i++)
        {
            var password = ServiceSecrets.GeneratePassword();
            Assert.Equal(ServiceSecrets.DefaultPasswordLength, password.Length);
            Assert.Matches("^[A-Za-z0-9]+$", password);
            Assert.True(ServiceValidation.TryValidatePassword(ServicePasswordPolicy.MssqlComplex, password, out _));
            Assert.True(ServiceValidation.TryValidatePassword(ServicePasswordPolicy.Standard, password, out _));
        }

        Assert.Matches("^[0-9a-f]{64}$", ServiceSecrets.GenerateKey());
    }

    [Theory]
    [InlineData("Ana Veritabanı", "ana-veritabani")]
    [InlineData("  Redis Önbellek!! ", "redis-onbellek")]
    [InlineData("***", "service")]
    public void Slugs_are_derived_from_names(string name, string expected)
    {
        var slug = ManagedServiceNames.Slugify(name);
        Assert.Equal(expected, slug);
        Assert.True(ManagedServiceNames.IsValidSlug(slug));
        Assert.Equal($"sm-svc-{expected}", ManagedServiceNames.ContainerName(slug));
        Assert.Equal($"sm-svc-{expected}-data", ManagedServiceNames.VolumeName(slug));
    }

    [Fact]
    public void Slug_suffix_keeps_length_limit()
    {
        var slug = ManagedServiceNames.Slugify(new string('a', 80));
        Assert.Equal(ManagedServiceNames.MaxSlugLength, slug.Length);
        var suffixed = ManagedServiceNames.WithSuffix(slug, 12);
        Assert.EndsWith("-12", suffixed);
        Assert.True(suffixed.Length <= ManagedServiceNames.MaxSlugLength);
        Assert.True(ManagedServiceNames.IsValidSlug(suffixed));
    }
}
