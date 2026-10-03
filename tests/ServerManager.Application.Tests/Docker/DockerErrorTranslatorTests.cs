using ServerManager.Application.DTOs.Ssh;
using ServerManager.Infrastructure.Docker;

namespace ServerManager.Application.Tests.Docker;

public class DockerErrorTranslatorTests
{
    private static string Translate(string stderr, int? exitCode = 1, bool timedOut = false) =>
        DockerErrorTranslator.Translate(new RemoteCommandOutput { ExitCode = exitCode, Stderr = stderr, TimedOut = timedOut });

    [Fact]
    public void Timeout_has_dedicated_message()
    {
        Assert.Equal("Docker komutu zaman aşımına uğradı.", Translate(string.Empty, null, timedOut: true));
    }

    [Theory]
    [InlineData("bash: docker: command not found", 127)]
    [InlineData("sh: docker: not found", 127)]
    [InlineData("", 127)]
    public void Missing_docker_is_detected(string stderr, int exitCode)
    {
        Assert.Equal("Sunucuda Docker kurulu değil veya PATH'te bulunamadı.", Translate(stderr, exitCode));
    }

    [Theory]
    [InlineData("Error response from daemon: No such container: web2", "Container bulunamadı.")]
    [InlineData("Error response from daemon: No such image: foo:latest", "Image bulunamadı.")]
    [InlineData("Error response from daemon: get nope: no such volume", "Volume bulunamadı.")]
    [InlineData("Error response from daemon: remove webdata: volume is in use - [7d0d]", "Volume kullanımda. Önce bağlı container'ları kaldırın.")]
    [InlineData("Error response from daemon: error while removing network: network appnet id ea60 has active endpoints", "Network'e bağlı container'lar var. Önce bağlantıları kaldırın.")]
    [InlineData("Error response from daemon: pull access denied for nope/x, repository does not exist", "Image bulunamadı veya registry erişim izni yok.")]
    [InlineData("Error response from daemon: manifest for nginx:nope not found: manifest unknown", "Bu etikete ait image bulunamadı.")]
    [InlineData("Error response from daemon: Conflict. The container name \"/cache\" is already in use by container", "Bu ad zaten kullanılıyor.")]
    [InlineData("Error response from daemon: network with name appnet already exists", "Bu ad zaten kullanılıyor.")]
    [InlineData("Error response from daemon: Pool overlaps with other one on this address space", "Subnet başka bir network ile çakışıyor.")]
    [InlineData("Error response from daemon: Container 7d0d is not running", "Container çalışmıyor.")]
    [InlineData("Error response from daemon: conflict: unable to remove repository reference \"nginx:alpine\" (must force) - container 7d0dac637d87 is using its referenced image a2b80c421aaa", "Image bir container tarafından kullanılıyor. Önce container'ı silin veya \"zorla sil\" seçeneğini işaretleyin.")]
    [InlineData("Error response from daemon: conflict: unable to delete a2b80c421aaa (cannot be forced) - image is being used by running container 7d0dac637d87", "Image çalışan bir container tarafından kullanılıyor. Önce container'ı durdurup silin.")]
    [InlineData("permission denied while trying to connect to the Docker daemon socket at unix:///var/run/docker.sock", "Kullanıcının Docker'a erişim yetkisi yok. Kullanıcıyı docker grubuna ekleyin veya sunucu ayarlarında sudo kullanımını açın.")]
    [InlineData("Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?", "Docker servisi çalışmıyor veya erişilemiyor.")]
    [InlineData("sudo: a password is required", "sudo parola istiyor. Sunucu ayarlarına sudo parolasını girin.")]
    [InlineData("Sorry, try again.\nsudo: 3 incorrect password attempts", "Sudo parolası hatalı.")]
    [InlineData("deploy is not in the sudoers file.", "Kullanıcının sudo yetkisi yok.")]
    public void Known_errors_are_translated(string stderr, string expected)
    {
        Assert.Equal(expected, Translate(stderr));
    }

    [Fact]
    public void Specific_not_found_messages_win_over_generic_one()
    {
        Assert.Equal("Bu etikete ait image bulunamadı.", Translate("manifest for x:y not found: manifest unknown"));
        Assert.Equal("Kayıt bulunamadı.", Translate("Error response from daemon: network nope not found"));
    }

    [Fact]
    public void Unknown_errors_show_first_line_without_daemon_prefix()
    {
        Assert.Equal("Docker: something odd happened", Translate("Error response from daemon: something odd happened\nsecond line"));
    }

    [Fact]
    public void Long_unknown_errors_are_truncated()
    {
        var message = Translate(new string('x', 500));

        Assert.Equal("Docker: ".Length + 300 + 1, message.Length);
        Assert.EndsWith("…", message);
    }

    [Fact]
    public void Empty_stderr_reports_exit_code()
    {
        Assert.Equal("Docker komutu başarısız oldu (çıkış kodu 2).", Translate(string.Empty, 2));
        Assert.Equal("Docker komutu başarısız oldu (çıkış kodu ?).", Translate("  \n", null));
    }
}
