using FluentValidation;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.Deployments;

public sealed class DomainFormDtoValidator : AbstractValidator<DomainFormDto>
{
    public DomainFormDtoValidator()
    {
        RuleFor(x => x.Host).Custom((host, context) =>
        {
            if (!DomainNames.TryNormalizeHost(host, out _, out var error))
                context.AddFailure(error!);
        });

        RuleFor(x => x.Path).Custom((path, context) =>
        {
            if (!DomainNames.TryNormalizePath(path, out _, out var error))
                context.AddFailure(error!);
        });

        RuleFor(x => x.ContainerPort)
            .InclusiveBetween(1, 65535).WithMessage("Container portu 1 ile 65535 arasında olmalıdır.");

        RuleFor(x => x.TlsMode)
            .IsInEnum().WithMessage("Sertifika türü seçin.");

        RuleFor(x => x.ServiceName)
            .Must(name => string.IsNullOrWhiteSpace(name) || DomainNames.IsValidServiceName(name.Trim()))
            .WithMessage("Compose servis adı harf veya rakamla başlamalı; harf, rakam, nokta, alt çizgi ve tire kullanılabilir.");

        RuleFor(x => x.CertificatePem)
            .MaximumLength(DomainNames.MaxCertificateLength).WithMessage("Sertifika çok uzun.");

        RuleFor(x => x.PrivateKey)
            .MaximumLength(DomainNames.MaxCertificateLength).WithMessage("Özel anahtar çok uzun.");

        RuleFor(x => x).Custom((dto, context) =>
        {
            if (dto.TlsMode != DeploymentTlsMode.Custom)
                return;

            var hasCertificate = !string.IsNullOrWhiteSpace(dto.CertificatePem);
            var hasKey = !string.IsNullOrWhiteSpace(dto.PrivateKey);
            var creating = dto.Id is null || dto.Id == Guid.Empty;
            if (creating && (!hasCertificate || !hasKey))
            {
                context.AddFailure(nameof(DomainFormDto.CertificatePem), "Özel sertifika ve özel anahtar birlikte yapıştırılmalıdır.");
                return;
            }

            if (hasCertificate != hasKey)
                context.AddFailure(nameof(DomainFormDto.CertificatePem), "Sertifika değiştirilecekse sertifika ve özel anahtar birlikte girilmelidir. Boş bırakmak kayıtlı olanı korur.");

            if (hasCertificate && !DomainNames.IsCertificatePem(dto.CertificatePem))
                context.AddFailure(nameof(DomainFormDto.CertificatePem), "Sertifika PEM biçiminde olmalıdır (BEGIN CERTIFICATE).");

            if (hasKey && !DomainNames.IsPrivateKeyPem(dto.PrivateKey))
                context.AddFailure(nameof(DomainFormDto.PrivateKey), "Özel anahtar PEM biçiminde olmalıdır (BEGIN PRIVATE KEY).");
        });
    }
}
