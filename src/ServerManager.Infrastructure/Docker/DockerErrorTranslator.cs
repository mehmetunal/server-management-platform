using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Infrastructure.Docker;

internal static class DockerErrorTranslator
{
    private const int MaxRawMessageLength = 300;

    private static readonly (string Pattern, string Message)[] KnownErrors =
    [
        ("a password is required", "sudo parola istiyor. Sunucu ayarlarına sudo parolasını girin."),
        ("incorrect password", "Sudo parolası hatalı."),
        ("Sorry, try again", "Sudo parolası hatalı."),
        ("is not in the sudoers", "Kullanıcının sudo yetkisi yok."),
        ("is not allowed to execute", "Kullanıcının docker komutunu sudo ile çalıştırma izni yok."),
        ("permission denied while trying to connect to the docker", "Kullanıcının Docker'a erişim yetkisi yok. Kullanıcıyı docker grubuna ekleyin veya sunucu ayarlarında sudo kullanımını açın."),
        ("Cannot connect to the Docker daemon", "Docker servisi çalışmıyor veya erişilemiyor."),
        ("No such container", "Container bulunamadı."),
        ("No such image", "Image bulunamadı."),
        ("no such volume", "Volume bulunamadı."),
        ("volume is in use", "Volume kullanımda. Önce bağlı container'ları kaldırın."),
        ("has active endpoints", "Network'e bağlı container'lar var. Önce bağlantıları kaldırın."),
        ("pull access denied", "Image bulunamadı veya registry erişim izni yok."),
        ("manifest unknown", "Bu etikete ait image bulunamadı."),
        ("You cannot remove a running container", "Çalışan container silinemez. Önce durdurun veya \"zorla sil\" seçeneğini işaretleyin."),
        ("cannot remove container", "Container silinemedi. Önce durdurun veya \"zorla sil\" seçeneğini işaretleyin."),
        ("cannot be forced", "Image çalışan bir container tarafından kullanılıyor. Önce container'ı durdurup silin."),
        ("is being used by", "Image bir container tarafından kullanılıyor. Önce container'ı silin veya \"zorla sil\" seçeneğini işaretleyin."),
        ("is using its referenced image", "Image bir container tarafından kullanılıyor. Önce container'ı silin veya \"zorla sil\" seçeneğini işaretleyin."),
        ("image is referenced in multiple repositories", "Image birden fazla etiketle kullanılıyor. Etiketi seçerek silin veya \"zorla sil\" seçeneğini işaretleyin."),
        ("is already in use", "Bu ad zaten kullanılıyor."),
        ("already exists", "Bu ad zaten kullanılıyor."),
        ("Pool overlaps", "Subnet başka bir network ile çakışıyor."),
        ("is not running", "Container çalışmıyor."),
        ("is not paused", "Container duraklatılmış değil."),
        ("is already paused", "Container zaten duraklatılmış."),
        ("is paused", "Container duraklatılmış. Önce devam ettirin."),
        ("is restarting", "Container yeniden başlıyor. Biraz bekleyip tekrar deneyin."),
        ("not found", "Kayıt bulunamadı.")
    ];

    public static string Translate(RemoteCommandOutput output)
    {
        if (output.TimedOut)
            return "Docker komutu zaman aşımına uğradı.";

        var stderr = output.Stderr;
        if (output.ExitCode == 127 || stderr.Contains("docker: command not found", StringComparison.OrdinalIgnoreCase)
                                   || stderr.Contains("docker: not found", StringComparison.OrdinalIgnoreCase))
            return "Sunucuda Docker kurulu değil veya PATH'te bulunamadı.";

        foreach (var (pattern, message) in KnownErrors)
        {
            if (stderr.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return message;
        }

        var firstLine = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(firstLine))
            return $"Docker komutu başarısız oldu (çıkış kodu {output.ExitCode?.ToString() ?? "?"}).";

        if (firstLine.StartsWith("Error response from daemon:", StringComparison.Ordinal))
            firstLine = firstLine["Error response from daemon:".Length..].Trim();

        return "Docker: " + (firstLine.Length > MaxRawMessageLength ? firstLine[..MaxRawMessageLength] + "…" : firstLine);
    }
}
