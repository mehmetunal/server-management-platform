using FluentValidation;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Application.Validators.Deployments;

public sealed class RemoteBranchQueryDtoValidator : AbstractValidator<RemoteBranchQueryDto>
{
    public RemoteBranchQueryDtoValidator()
    {
        RuleFor(x => x.ServerId)
            .NotEmpty().WithMessage("Dalları okumak için önce hedef sunucuyu seçin.");

        RuleFor(x => x.GitProvider)
            .IsInEnum().WithMessage("Geçerli bir Git sağlayıcısı seçin.");

        RuleFor(x => x.RepositoryUrl).Custom((url, context) =>
        {
            if (!GitRepositoryUrls.TryValidate(url?.Trim(), out var error))
                context.AddFailure(nameof(RemoteBranchQueryDto.RepositoryUrl), error!);
        });

        RuleFor(x => x.GitUsername)
            .MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir.")
            .When(x => !string.IsNullOrWhiteSpace(x.GitUsername));

        RuleFor(x => x.AccessToken)
            .MaximumLength(4096).WithMessage("Erişim anahtarı en fazla 4096 karakter olabilir.")
            .Must(token => !token!.Any(char.IsControl)).WithMessage("Erişim anahtarı satır sonu veya kontrol karakteri içeremez.")
            .Must((dto, _) => GitRepositoryUrls.IsHttps(dto.RepositoryUrl?.Trim())).WithMessage("Erişim anahtarı yalnızca https:// depo adresleriyle kullanılabilir.")
            .When(x => !string.IsNullOrEmpty(x.AccessToken));
    }
}
