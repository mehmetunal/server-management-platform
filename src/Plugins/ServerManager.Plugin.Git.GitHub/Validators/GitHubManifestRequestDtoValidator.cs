using FluentValidation;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Validators;

public class GitHubManifestRequestDtoValidator : AbstractValidator<GitHubManifestRequestDto>
{
    /// <summary>GitHub App adları en fazla 34 karakter olabilir.</summary>
    public const int MaxNameLength = 34;

    public GitHubManifestRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Uygulama adı zorunludur.")
            .MaximumLength(MaxNameLength).WithMessage($"Uygulama adı en fazla {MaxNameLength} karakter olabilir.")
            .Matches(@"^[\p{L}\p{N} ._\-]+$").WithMessage("Uygulama adında yalnızca harf, rakam, boşluk, nokta, tire ve alt çizgi kullanılabilir.");

        RuleFor(x => x.Organization)
            .Matches("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$").WithMessage("Kurum adı geçersiz (GitHub kullanıcı/kurum adı).")
            .When(x => !string.IsNullOrWhiteSpace(x.Organization));
    }
}
