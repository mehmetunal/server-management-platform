using ServerManager.Application.ManagedServices;

namespace ServerManager.Application.Tests.ManagedServices;

public class SecretMaskerTests
{
    [Fact]
    public void Masks_secrets_and_their_url_encoded_form()
    {
        var masker = new SecretMasker(["p@ss/word123", null, "ab"]);

        Assert.Equal("pw=**** url=postgres://u:****@h ab", masker.Apply("pw=p@ss/word123 url=postgres://u:p%40ss%2Fword123@h ab"));
    }

    [Fact]
    public void Secret_split_across_chunks_is_still_masked()
    {
        var masker = new SecretMasker(["SuperSecret42"]);

        var first = masker.Push("line one\nvalue=Super");
        var second = masker.Push("Secret42 end\nrest");
        var tail = masker.Flush();

        Assert.Equal("line one\n", first);
        Assert.Equal("value=**** end\n", second);
        Assert.Equal("rest", tail);
    }

    [Fact]
    public void Without_secrets_output_passes_through()
    {
        var masker = new SecretMasker([]);
        Assert.Equal("partial", masker.Push("partial"));
    }

    [Fact]
    public void Very_long_lines_are_not_held_forever()
    {
        var masker = new SecretMasker(["SuperSecret42"]);
        var progress = new string('#', 5000);

        Assert.Equal(progress, masker.Push(progress));
    }

    [Fact]
    public void Credentials_to_string_hides_password()
    {
        var credentials = new ServiceCredentials { Username = "app", Password = "SuperSecret42" };

        Assert.DoesNotContain("SuperSecret42", credentials.ToString());
        Assert.Equal(credentials, ServiceCredentials.FromJson(credentials.ToJson()));
    }
}
