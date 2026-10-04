using FluentValidation;
using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Application.Validators.Backups;

/// <summary>Yedek türüne bağlı zorunlu alanlar serviste, yedeğin türüne göre kontrol edilir.</summary>
public sealed class BackupRestoreDtoValidator : AbstractValidator<BackupRestoreDto>
{
    public BackupRestoreDtoValidator()
    {
        RuleFor(x => x.RunId)
            .NotEmpty().WithMessage("Geri yüklenecek yedek seçilmedi.");

        RuleFor(x => x.TargetServerId)
            .NotEmpty().WithMessage("Hedef sunucuyu seçin.");

        RuleFor(x => x.TargetDirectory)
            .Must(path => BackupPaths.Normalize(path) is { } n && !BackupPaths.IsPseudoFileSystem(n))
            .When(x => !string.IsNullOrWhiteSpace(x.TargetDirectory))
            .WithMessage("Hedef klasör '/' ile başlayan mutlak bir yol olmalı; '..' ve /proc, /sys, /dev kullanılamaz.");

        RuleFor(x => x.TargetVolume)
            .Matches(BackupInputPatterns.DockerName).When(x => !string.IsNullOrWhiteSpace(x.TargetVolume))
            .WithMessage("Volume adı harf veya rakamla başlamalı; yalnızca harf, rakam, '_', '.', '-' içerebilir.");

        RuleFor(x => x.TargetContainer)
            .Matches(BackupInputPatterns.DockerName).When(x => !string.IsNullOrWhiteSpace(x.TargetContainer))
            .WithMessage("Container adı harf veya rakamla başlamalı; yalnızca harf, rakam, '_', '.', '-' içerebilir.");

        RuleFor(x => x.TargetDatabase)
            .Matches(BackupInputPatterns.DatabaseName).When(x => !string.IsNullOrWhiteSpace(x.TargetDatabase))
            .WithMessage("Veritabanı adı yalnızca harf, rakam, '_', '-', '.', '$' içerebilir ve '-' ile başlayamaz.");

        RuleFor(x => x.Confirmed)
            .Equal(true).WithMessage("Hedefteki verinin üzerine yazılacağını onaylayın.");
    }
}
