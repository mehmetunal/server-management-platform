using System.Text.RegularExpressions;
using FluentValidation;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.Deployments;

public abstract partial class ProjectFormDtoValidator<T> : AbstractValidator<T> where T : ProjectFormDto
{
    public const int MaxCommandLength = 4000;

    protected ProjectFormDtoValidator()
    {
        RuleFor(x => x.ServerId)
            .NotEmpty().WithMessage("Hedef sunucuyu seçin.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Proje adı zorunludur.")
            .MaximumLength(128).WithMessage("Proje adı en fazla 128 karakter olabilir.")
            .Matches(NamePattern()).WithMessage("Proje adı yalnızca harf, rakam, boşluk, nokta, alt çizgi ve tire içerebilir.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Açıklama en fazla 1000 karakter olabilir.");

        RuleFor(x => x.GitProvider)
            .IsInEnum().WithMessage("Geçerli bir Git sağlayıcısı seçin.");

        RuleFor(x => x.GitSource)
            .Must(source => GitSourceKeys.TryParse(source?.Trim(), out _, out _))
            .WithMessage("Geçerli bir Git bağlantısı seçin.")
            .When(x => x.UsesIntegration);

        RuleFor(x => x.GitRepository)
            .Must(repository => GitSourceKeys.IsValidRepository(repository?.Trim()))
            .WithMessage("Bağlantıdaki depolardan birini seçin.")
            .When(x => x.UsesIntegration);

        RuleFor(x => x.RepositoryUrl).Custom((url, context) =>
        {
            if (!GitRepositoryUrls.TryValidate(url?.Trim(), out var error))
                context.AddFailure(nameof(ProjectFormDto.RepositoryUrl), error!);
        }).When(x => !x.UsesIntegration);

        RuleFor(x => x.Branch)
            .Must(branch => GitRefs.IsValidBranch(branch?.Trim()))
            .WithMessage("Geçerli bir dal adı girin (ör. main, release/1.2).");

        RuleFor(x => x.GitUsername)
            .MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir.")
            .Matches(UsernamePattern()).WithMessage("Kullanıcı adı harf, rakam ve . _ @ + - içerebilir.")
            .When(x => !string.IsNullOrWhiteSpace(x.GitUsername));

        RuleFor(x => x.AccessToken)
            .MaximumLength(4096).WithMessage("Erişim anahtarı en fazla 4096 karakter olabilir.")
            .Must(token => !token!.Any(char.IsControl)).WithMessage("Erişim anahtarı satır sonu veya kontrol karakteri içeremez.")
            .Must((dto, _) => GitRepositoryUrls.IsHttps(dto.RepositoryUrl?.Trim())).WithMessage("Erişim anahtarı yalnızca https:// depo adresleriyle kullanılabilir.")
            .When(x => !string.IsNullOrEmpty(x.AccessToken) && !x.UsesIntegration);

        RuleFor(x => x.DeployPath).Custom((path, context) =>
        {
            if (!DeployPaths.TryValidate(path?.Trim(), out var error))
                context.AddFailure(nameof(ProjectFormDto.DeployPath), error!);
        });

        RuleFor(x => x.BuildType)
            .IsInEnum().WithMessage("Geçerli bir build türü seçin.");

        RuleFor(x => x.ComposeFile)
            .Must(file => DeployPaths.IsValidRelativeFile(file?.Trim()))
            .WithMessage("Proje klasörüne göre compose dosyası yolunu girin (ör. docker-compose.yml, deploy/compose.prod.yml).")
            .When(x => x.BuildType == DeploymentBuildType.DockerCompose);

        RuleFor(x => x.DockerfilePath)
            .Must(file => DeployPaths.IsValidRelativeFile(file?.Trim()))
            .WithMessage("Proje klasörüne göre Dockerfile yolunu girin (ör. Dockerfile, src/Api/Dockerfile).")
            .When(x => x.BuildType == DeploymentBuildType.Dockerfile);

        RuleFor(x => x.PortMappings).Custom((mappings, context) =>
        {
            if (!PortMappings.TryParse(mappings, out _, out var error))
                context.AddFailure(nameof(ProjectFormDto.PortMappings), error!);
        });

        RuleFor(x => x.BuildCommand)
            .MaximumLength(MaxCommandLength).WithMessage($"Build komutu en fazla {MaxCommandLength} karakter olabilir.")
            .Must(command => !command!.Contains('\0')).WithMessage("Build komutu geçersiz karakter içeriyor.")
            .When(x => !string.IsNullOrEmpty(x.BuildCommand));

        RuleFor(x => x.DeployCommand)
            .NotEmpty().WithMessage("Komut türündeki projelerde deploy komutu zorunludur.")
            .When(x => x.BuildType == DeploymentBuildType.Commands);

        RuleFor(x => x.DeployCommand)
            .MaximumLength(MaxCommandLength).WithMessage($"Deploy komutu en fazla {MaxCommandLength} karakter olabilir.")
            .Must(command => !command!.Contains('\0')).WithMessage("Deploy komutu geçersiz karakter içeriyor.")
            .When(x => !string.IsNullOrEmpty(x.DeployCommand));

        RuleFor(x => x.Environment).Custom((environment, context) =>
        {
            if (!EnvironmentFile.TryParse(environment, out _, out var error))
                context.AddFailure(nameof(ProjectFormDto.Environment), error!);
        });
    }

    [GeneratedRegex(@"^[\p{L}\p{N} ._\-]+$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[A-Za-z0-9._@+\-]+$")]
    private static partial Regex UsernamePattern();
}
