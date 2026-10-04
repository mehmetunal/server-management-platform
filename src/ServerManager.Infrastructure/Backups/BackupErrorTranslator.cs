using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Infrastructure.Backups;

public static class BackupErrorTranslator
{
    private const int MaxMessageLength = 400;

    public static string Translate(RemoteCommandOutput output, string operation)
    {
        if (output.TimedOut)
            return $"{operation} zaman aşımına uğradı.";

        var stderr = output.Stderr ?? string.Empty;
        var lastLine = LastLine(stderr);

        if (output.ExitCode == BackupCommands.SourceMissingExitCode && lastLine is not null)
            return lastLine;
        if (output.ExitCode == BackupCommands.ToolMissingExitCode)
            return $"Gerekli istemci bulunamadı: {lastLine ?? "bilinmiyor"}. Container'da veya sunucuda kurulu olmalıdır.";

        if (Contains(stderr, "a password is required") || Contains(stderr, "a terminal is required"))
            return "sudo parola istiyor; sunucu ayarlarında sudo parolasını girin.";
        if (Contains(stderr, "incorrect password attempt") || Contains(stderr, "Sorry, try again"))
            return "sudo parolası hatalı.";
        if (Contains(stderr, "docker.sock") && Contains(stderr, "permission denied"))
            return "Docker'a erişim izni yok; sunucu ayarlarında sudo kullanımını açın.";
        if (Contains(stderr, "No such container"))
            return "Container bulunamadı veya çalışmıyor.";
        if (Contains(stderr, "is not running"))
            return "Container çalışmıyor.";
        if (Contains(stderr, "password authentication failed") || Contains(stderr, "Access denied for user"))
            return "Veritabanı kullanıcı adı veya parolası hatalı.";
        if (Contains(stderr, "Unknown database") || (Contains(stderr, "database") && Contains(stderr, "does not exist")))
            return "Veritabanı bulunamadı.";
        if (Contains(stderr, "role") && Contains(stderr, "does not exist"))
            return "Veritabanı kullanıcısı bulunamadı.";
        if (Contains(stderr, "No space left on device"))
            return "Sunucuda disk alanı kalmadı.";
        if (Contains(stderr, "docker: not found") || Contains(stderr, "docker: command not found"))
            return "Sunucuda Docker kurulu değil.";
        if (Contains(stderr, "unrecognized option") && Contains(stderr, "warning"))
            return "Sunucudaki tar GNU tar değil; GNU tar kurulu olmalıdır.";
        if (Contains(stderr, "not in gzip format") || Contains(stderr, "unexpected end of file"))
            return "Yedek dosyası açılamadı (gzip verisi bozuk veya eksik).";

        var detail = lastLine is null ? string.Empty : $": {lastLine}";
        return TextHelper.Truncate($"{operation} başarısız (çıkış kodu {output.ExitCode?.ToString() ?? "yok"}){detail}", MaxMessageLength)!;
    }

    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string? LastLine(string text)
    {
        var line = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(l => !l.StartsWith("[sudo]", StringComparison.Ordinal));
        return line is null ? null : TextHelper.Truncate(line, 300);
    }
}
