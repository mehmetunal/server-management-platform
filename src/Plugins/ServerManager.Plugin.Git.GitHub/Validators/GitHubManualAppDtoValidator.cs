using FluentValidation;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Validators;

public class GitHubManualAppDtoValidator : AbstractValidator<GitHubManualAppDto>
{
    private const int MaxPrivateKeyLength = 16_000;

    public GitHubManualAppDtoValidator()
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0).WithMessage("GitHub App kimliğini (App ID) girin.");

        RuleFor(x => x.PrivateKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Özel anahtar zorunludur.")
            .MaximumLength(MaxPrivateKeyLength).WithMessage("Özel anahtar çok uzun.")
            .Must(GitHubJwt.IsValidPrivateKey).WithMessage("GitHub'dan indirilen .pem dosyasının içeriğini (en az 2048 bit RSA) yapıştırın.");
    }
}
