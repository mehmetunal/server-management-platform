using FluentValidation;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Validators;

public class DokploySettingsDtoValidator : AbstractValidator<DokploySettingsDto>
{
    public const int MaxApiKeyLength = 512;

    public DokploySettingsDtoValidator()
    {
        RuleFor(x => x.BaseUrl)
            .NotEmpty().WithMessage("Dokploy adresini girin.")
            .Must(url => DokployUrls.TryNormalize(url, out _))
            .WithMessage("Geçerli bir http veya https adresi girin (ör. http://203.0.113.10:3000). Kullanıcı bilgisi, sorgu ve # içeremez.");

        RuleFor(x => x.ApiKey)
            .MaximumLength(MaxApiKeyLength).WithMessage($"API anahtarı en fazla {MaxApiKeyLength} karakter olabilir.")
            .Must(key => !key!.Any(char.IsWhiteSpace)).WithMessage("API anahtarı boşluk içeremez.")
            .When(x => !string.IsNullOrEmpty(x.ApiKey));
    }
}
