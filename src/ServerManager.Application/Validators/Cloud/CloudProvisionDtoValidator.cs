using FluentValidation;
using ServerManager.Application.Cloud;
using ServerManager.Application.DTOs.Cloud;

namespace ServerManager.Application.Validators.Cloud;

public sealed class CloudProvisionDtoValidator : AbstractValidator<CloudProvisionDto>
{
    public CloudProvisionDtoValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithMessage("Hesap seçin.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sunucu adı zorunludur.")
            .Matches("^[a-zA-Z0-9]([a-zA-Z0-9.-]{0,61}[a-zA-Z0-9])?$")
            .WithMessage("Sunucu adı harf, rakam, nokta ve tire içerebilir; harf veya rakamla başlayıp bitmelidir (en fazla 63 karakter).");

        RuleFor(x => x.Region).NotEmpty().WithMessage("Bölge seçin.");
        RuleFor(x => x.Size).NotEmpty().WithMessage("Sunucu tipi seçin.");
        RuleFor(x => x.Image).NotEmpty().WithMessage("İşletim sistemi imajı seçin.");

        RuleFor(x => x.SshPublicKey)
            .Must(CloudInitBuilder.IsValidPublicKey)
            .When(x => !string.IsNullOrWhiteSpace(x.SshPublicKey))
            .WithMessage("Geçerli bir OpenSSH genel anahtarı girin (ör. ssh-ed25519 AAAA… yorum).");
    }
}
