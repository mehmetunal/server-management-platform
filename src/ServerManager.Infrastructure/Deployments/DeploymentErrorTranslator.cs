using ServerManager.Application.DTOs.Ssh;
using ServerManager.Infrastructure.Docker;

namespace ServerManager.Infrastructure.Deployments;

public static class DeploymentErrorTranslator
{
    private const int MaxRawMessageLength = 300;

    private static readonly (string Pattern, string Message)[] GitErrors =
    [
        ("terminal prompts disabled", "Depoya erişim reddedildi. Özel depolar için erişim anahtarı (token) girin."),
        ("could not read Username", "Depoya erişim reddedildi. Özel depolar için erişim anahtarı (token) girin."),
        ("could not read Password", "Depoya erişim reddedildi. Erişim anahtarını kontrol edin."),
        ("Authentication failed", "Depoya erişim reddedildi. Erişim anahtarının geçerli olduğunu ve depoyu okuma yetkisi olduğunu kontrol edin."),
        ("Invalid username or password", "Depoya erişim reddedildi. Erişim anahtarını kontrol edin."),
        ("HTTP Basic: Access denied", "Depoya erişim reddedildi. Erişim anahtarını kontrol edin."),
        ("The requested URL returned error: 403", "Erişim anahtarının bu depoyu okuma yetkisi yok."),
        ("Repository not found", "Depo bulunamadı veya erişim izni yok."),
        ("does not appear to be a git repository", "Depo bulunamadı veya erişim izni yok."),
        ("The requested URL returned error: 404", "Depo bulunamadı veya erişim izni yok."),
        ("Could not resolve host", "Git sunucusunun adresi çözümlenemedi; hedef sunucunun DNS ve internet erişimini kontrol edin."),
        ("Could not resolve hostname", "Git sunucusunun adresi çözümlenemedi; hedef sunucunun DNS ve internet erişimini kontrol edin."),
        ("Temporary failure in name resolution", "Git sunucusunun adresi çözümlenemedi; hedef sunucunun DNS ve internet erişimini kontrol edin."),
        ("Connection timed out", "Git sunucusuna bağlanılamadı (zaman aşımı)."),
        ("Connection refused", "Git sunucusu bağlantıyı reddetti."),
        ("Failed to connect", "Git sunucusuna bağlanılamadı."),
        ("SSL certificate problem", "Git sunucusunun TLS sertifikası doğrulanamadı."),
        ("Host key verification failed", "Git sunucusunun SSH host key'i doğrulanamadı."),
        ("Permission denied (publickey", "Hedef sunucunun SSH anahtarı depoda yetkili değil; sunucudaki kullanıcının açık anahtarını depoya deploy key olarak ekleyin."),
        ("couldn't find remote ref", "Dal depoda bulunamadı."),
        ("not our ref", "Commit depoda bulunamadı veya Git sunucusu commit ile çekmeye izin vermiyor."),
        ("unadvertised object", "Commit depoda bulunamadı veya Git sunucusu commit ile çekmeye izin vermiyor."),
        ("dubious ownership", "Klasör başka bir kullanıcıya ait olduğu için git çalışmayı reddetti; klasörün sahibini SSH kullanıcısı yapın."),
        ("Permission denied", "Deploy klasörüne yazma izni yok; klasörü SSH kullanıcısının yazabileceği bir yere alın veya sahibini değiştirin."),
        ("Read-only file system", "Deploy klasörü salt okunur bir dosya sisteminde.")
    ];

    private static readonly string[] DockerAccessPatterns =
    [
        "a password is required",
        "incorrect password",
        "Sorry, try again",
        "is not in the sudoers",
        "is not allowed to execute",
        "permission denied while trying to connect to the docker",
        "Cannot connect to the Docker daemon",
        "docker: not found",
        "docker: command not found",
        "is not a docker command"
    ];

    public static string TranslateGit(RemoteCommandOutput output, TimeSpan timeout)
    {
        if (output.TimedOut)
            return $"Git işlemi {Describe(timeout)} içinde bitmedi.";

        var stderr = output.Stderr;
        if (output.ExitCode == 127 || stderr.Contains("git: not found", StringComparison.OrdinalIgnoreCase)
                                   || stderr.Contains("git: command not found", StringComparison.OrdinalIgnoreCase))
            return "Sunucuda git kurulu değil veya PATH'te bulunamadı.";

        foreach (var (pattern, message) in GitErrors)
        {
            if (stderr.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return message;
        }

        var fatal = Lines(stderr).LastOrDefault(l => l.StartsWith("fatal:", StringComparison.Ordinal) || l.StartsWith("error:", StringComparison.Ordinal));
        var line = fatal ?? Lines(stderr).LastOrDefault();
        return line is null
            ? $"Git işlemi başarısız oldu (çıkış kodu {output.ExitCode?.ToString() ?? "?"})."
            : "Git: " + Clip(line);
    }

    public static string TranslateDocker(RemoteCommandOutput output, string step, TimeSpan timeout)
    {
        if (output.TimedOut)
            return $"{step} {Describe(timeout)} içinde bitmedi.";

        var stderr = output.Stderr;
        if (output.ExitCode == 127 || DockerAccessPatterns.Any(p => stderr.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return DockerErrorTranslator.Translate(output);

        var lines = Lines(stderr).Where(l => !l.StartsWith('#')).ToList();
        var solve = lines.LastOrDefault(l => l.Contains("failed to solve:", StringComparison.OrdinalIgnoreCase));
        if (solve is not null)
            return $"{step} başarısız: {Clip(solve[(solve.IndexOf("failed to solve:", StringComparison.OrdinalIgnoreCase) + "failed to solve:".Length)..].Trim())}";

        var daemon = lines.LastOrDefault(l => l.StartsWith("Error response from daemon:", StringComparison.Ordinal));
        if (daemon is not null)
            return $"{step} başarısız: {Clip(daemon["Error response from daemon:".Length..].Trim())}";

        var last = lines.LastOrDefault();
        return last is null
            ? $"{step} hata ile bitti (çıkış kodu {output.ExitCode?.ToString() ?? "?"}); ayrıntılar konsolda."
            : $"{step} başarısız: {Clip(last)}";
    }

    public static string TranslateCommand(RemoteCommandOutput output, string step, TimeSpan timeout)
    {
        if (output.TimedOut)
            return $"{step} komutu {Describe(timeout)} içinde bitmedi.";

        if (DockerAccessPatterns.Take(5).Any(p => output.Stderr.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return DockerErrorTranslator.Translate(output);

        if (output.ExitCode == 127)
        {
            var line = Lines(output.Stderr).LastOrDefault();
            return line is null ? $"{step} komutunda bulunamayan bir program var." : $"{step} komutunda program bulunamadı: {Clip(line)}";
        }

        return $"{step} komutu hata ile bitti (çıkış kodu {output.ExitCode?.ToString() ?? "?"}); ayrıntılar konsolda.";
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Clip(string value) =>
        value.Length > MaxRawMessageLength ? value[..MaxRawMessageLength] + "…" : value;

    private static string Describe(TimeSpan timeout) =>
        timeout.TotalMinutes >= 1 ? $"{timeout.TotalMinutes:0} dakika" : $"{timeout.TotalSeconds:0} saniye";
}
