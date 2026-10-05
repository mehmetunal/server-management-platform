using System.Globalization;
using FluentValidation;
using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.Backups;

public sealed class BackupJobFormDtoValidator : AbstractValidator<BackupJobFormDto>
{
    public const int MinPassphraseLength = 12;
    public const int MaxPassphraseLength = 256;
    public const int MaxDatabasePasswordLength = 256;

    public BackupJobFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("İş adı zorunludur.")
            .MaximumLength(128).WithMessage("İş adı en fazla 128 karakter olabilir.");

        RuleFor(x => x.ServerId)
            .NotEmpty().WithMessage("Sunucu seçin.");

        RuleFor(x => x.StorageId)
            .NotEmpty().WithMessage("Depolama hedefi seçin.");

        RuleFor(x => x.SourceType)
            .IsInEnum().WithMessage("Geçerli bir yedek türü seçin.");

        When(x => x.SourceType == BackupSourceType.Files, () =>
        {
            RuleFor(x => x.Paths)
                .NotEmpty().WithMessage("En az bir klasör veya dosya yolu girin.")
                .Must(paths => BackupPaths.SplitLines(paths).Count <= BackupPaths.MaxPathCount)
                    .WithMessage($"En fazla {BackupPaths.MaxPathCount} yol girilebilir.")
                .Must(paths => BackupPaths.SplitLines(paths).All(p => BackupPaths.Normalize(p) is { } n && n != "/" && !BackupPaths.IsPseudoFileSystem(n)))
                    .WithMessage("Her satır '/' ile başlayan mutlak bir yol olmalı; '..', kök dizin (/) ve /proc, /sys, /dev kullanılamaz.");

            RuleFor(x => x.Excludes)
                .Must(excludes => BackupPaths.SplitLines(excludes).Count <= BackupPaths.MaxExcludeCount)
                    .WithMessage($"En fazla {BackupPaths.MaxExcludeCount} hariç tutma kalıbı girilebilir.")
                .Must(excludes => BackupPaths.SplitLines(excludes).All(e => e.Length <= BackupPaths.MaxExcludeLength && !e.Any(char.IsControl)))
                    .WithMessage($"Hariç tutma kalıpları en fazla {BackupPaths.MaxExcludeLength} karakter olabilir.");
        });

        When(x => x.SourceType == BackupSourceType.DockerVolume, () =>
        {
            RuleFor(x => x.VolumeName)
                .NotEmpty().WithMessage("Volume adı zorunludur.")
                .Matches(BackupInputPatterns.DockerName).WithMessage("Volume adı harf veya rakamla başlamalı; yalnızca harf, rakam, '_', '.', '-' içerebilir.");
        });

        When(x => x.SourceType == BackupSourceType.Database, () =>
        {
            RuleFor(x => x.DatabaseEngine)
                .IsInEnum().WithMessage("Veritabanı türünü seçin.");

            RuleFor(x => x.DatabaseName)
                .NotEmpty().WithMessage("Veritabanı adı zorunludur.")
                .Matches(BackupInputPatterns.DatabaseName).WithMessage("Veritabanı adı yalnızca harf, rakam, '_', '-', '.', '$' içerebilir ve '-' ile başlayamaz.");

            RuleFor(x => x.DatabaseUser)
                .NotEmpty().WithMessage("Veritabanı kullanıcısı zorunludur.")
                .Matches(BackupInputPatterns.DatabaseUser).WithMessage("Kullanıcı adı yalnızca harf, rakam, '_', '-', '.', '@' içerebilir ve '-' ile başlayamaz.");

            RuleFor(x => x.DatabasePassword)
                .MaximumLength(MaxDatabasePasswordLength).WithMessage($"Parola en fazla {MaxDatabasePasswordLength} karakter olabilir.")
                .Must(BackupInputPatterns.IsValidDatabasePassword).WithMessage("Parola satır sonu veya NUL karakteri içeremez.");

            RuleFor(x => x.ContainerName)
                .Matches(BackupInputPatterns.DockerName).When(x => !string.IsNullOrWhiteSpace(x.ContainerName))
                .WithMessage("Container adı harf veya rakamla başlamalı; yalnızca harf, rakam, '_', '.', '-' içerebilir.");

            RuleFor(x => x.DatabaseHost)
                .Matches(BackupInputPatterns.Host).When(x => !string.IsNullOrWhiteSpace(x.DatabaseHost))
                .WithMessage("Sunucu adresi geçersiz.");

            RuleFor(x => x.DatabasePort)
                .InclusiveBetween(1, 65535).When(x => x.DatabasePort.HasValue)
                .WithMessage("Port 1 ile 65535 arasında olmalıdır.");
        });

        When(x => x.EncryptionEnabled, () =>
        {
            RuleFor(x => x.Passphrase)
                .NotEmpty().When(x => !x.HasStoredPassphrase)
                .WithMessage("Şifreleme parolası zorunludur.");

            RuleFor(x => x.Passphrase)
                .MinimumLength(MinPassphraseLength).WithMessage($"Şifreleme parolası en az {MinPassphraseLength} karakter olmalıdır.")
                .MaximumLength(MaxPassphraseLength).WithMessage($"Şifreleme parolası en fazla {MaxPassphraseLength} karakter olabilir.")
                .Equal(x => x.PassphraseConfirm).WithMessage("Şifreleme parolaları eşleşmiyor.")
                .When(x => !string.IsNullOrEmpty(x.Passphrase));
        });

        RuleFor(x => x.ScheduleType)
            .IsInEnum().WithMessage("Geçerli bir zamanlama seçin.");

        RuleFor(x => x.ScheduleIntervalHours)
            .InclusiveBetween(BackupSchedule.MinIntervalHours, BackupSchedule.MaxIntervalHours)
            .When(x => x.ScheduleType == BackupScheduleType.Hourly)
            .WithMessage($"Aralık {BackupSchedule.MinIntervalHours} ile {BackupSchedule.MaxIntervalHours} saat arasında olmalıdır.");

        RuleFor(x => x.ScheduleTime)
            .Must(time => TryParseTime(time, out _))
            .When(x => x.ScheduleType is BackupScheduleType.Daily or BackupScheduleType.Weekly)
            .WithMessage("Saati SS:dd biçiminde girin (ör. 03:00).");

        RuleFor(x => x.ScheduleDayOfWeek)
            .IsInEnum().When(x => x.ScheduleType == BackupScheduleType.Weekly)
            .WithMessage("Haftanın gününü seçin.");

        RuleFor(x => x.KeepLast)
            .InclusiveBetween(BackupRetention.MinKeepLast, BackupRetention.MaxKeepLast)
            .WithMessage($"Saklanacak yedek sayısı {BackupRetention.MinKeepLast} ile {BackupRetention.MaxKeepLast} arasında olmalıdır.");

        RuleFor(x => x.KeepDays)
            .InclusiveBetween(0, BackupRetention.MaxKeepDays)
            .WithMessage($"Saklama süresi 0 ile {BackupRetention.MaxKeepDays} gün arasında olmalıdır.");
    }

    public static bool TryParseTime(string? value, out int minuteOfDay)
    {
        minuteOfDay = 0;
        if (!TimeOnly.TryParseExact(value?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return false;

        minuteOfDay = time.Hour * 60 + time.Minute;
        return true;
    }
}
