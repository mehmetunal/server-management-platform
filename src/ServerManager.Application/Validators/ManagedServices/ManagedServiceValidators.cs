using FluentValidation;
using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.ManagedServices;

public sealed class CreateManagedServiceDtoValidator : AbstractValidator<CreateManagedServiceDto>
{
    public const int MaxNameLength = 64;

    public CreateManagedServiceDtoValidator(IOptions<ManagedServiceOptions> options, IServiceTemplateCatalog templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var allowPrivileged = options.Value.AllowPrivilegedHostPorts;

        RuleFor(x => x.ServerId).NotEmpty().WithMessage("Sunucu seçin.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Servis adı zorunludur.")
            .MaximumLength(MaxNameLength).WithMessage($"Servis adı en fazla {MaxNameLength} karakter olabilir.");

        RuleFor(x => x.TemplateKey)
            .Must(key => templates.Find(key) is not null).WithMessage("Geçerli bir servis türü seçin.");

        RuleFor(x => x.ImageTag)
            .Must(ServiceValidation.IsValidTag)
            .WithMessage("Sürüm etiketi harf, rakam, nokta, alt çizgi ve tire içerebilir (en fazla 128 karakter).");

        RuleFor(x => x.VolumeMode).IsInEnum().WithMessage("Geçersiz veri saklama türü.");

        RuleFor(x => x.HostDataPath)
            .Custom((path, context) =>
            {
                if (!ServiceValidation.TryValidateHostPath(path, out var error))
                    context.AddFailure(nameof(CreateManagedServiceDto.HostDataPath), error!);
            })
            .When(x => x.VolumeMode == ManagedServiceVolumeMode.HostPath);

        RuleFor(x => x).Custom((dto, context) =>
        {
            var template = templates.Find(dto.TemplateKey);
            if (template is null)
                return;

            foreach (var (property, message) in CredentialErrors(template, dto))
                context.AddFailure(property, message);

            foreach (var (property, message) in ManagedServiceSettingsRules.Validate(template, dto, allowPrivileged))
                context.AddFailure(property, message);
        });
    }

    private static IEnumerable<(string Property, string Message)> CredentialErrors(ServiceTemplate template, CreateManagedServiceDto dto)
    {
        var spec = template.Credentials;
        switch (spec.UsernameKind)
        {
            case ServiceUsernameKind.Name:
                if (!ServiceValidation.IsValidUsername(dto.Username))
                    yield return (nameof(dto.Username), "Kullanıcı adı harf veya alt çizgiyle başlamalı; harf, rakam, nokta, alt çizgi ve tire içerebilir (en fazla 63 karakter).");
                else if (spec.ReservedUsernames.Contains(dto.Username!, StringComparer.OrdinalIgnoreCase))
                    yield return (nameof(dto.Username), $"{dto.Username} kullanıcı adı ayrılmıştır; başka bir ad seçin.");
                break;
            case ServiceUsernameKind.Email:
                if (!ServiceValidation.IsValidEmail(dto.Username))
                    yield return (nameof(dto.Username), "Geçerli bir e-posta adresi girin.");
                break;
        }

        if (!ServiceValidation.TryValidatePassword(spec.PasswordPolicy, dto.Password, out var passwordError))
            yield return (nameof(dto.Password), passwordError!);

        if (spec.HasDatabase && !ServiceValidation.IsValidDatabaseName(dto.Database))
            yield return (nameof(dto.Database), "Veritabanı adı harf veya alt çizgiyle başlamalı; harf, rakam ve alt çizgi içerebilir (en fazla 63 karakter).");
    }
}

/// <summary>Ayarlar sekmesi; <see cref="UpdateManagedServiceDto"/> şablonu servis kaydından alınır ve doğrulayıcıya bağlamla verilir.</summary>
public sealed class UpdateManagedServiceDtoValidator : AbstractValidator<UpdateManagedServiceDto>
{
    public const string TemplateKey = "template";

    public UpdateManagedServiceDtoValidator(IOptions<ManagedServiceOptions> options)
    {
        var allowPrivileged = options.Value.AllowPrivilegedHostPorts;
        RuleFor(x => x).Custom((dto, context) =>
        {
            if (!context.RootContextData.TryGetValue(TemplateKey, out var value) || value is not ServiceTemplate template)
            {
                context.AddFailure(string.Empty, "Servis şablonu bulunamadı.");
                return;
            }

            foreach (var (property, message) in ManagedServiceSettingsRules.Validate(template, dto, allowPrivileged))
                context.AddFailure(property, message);
        });
    }
}

public sealed class UpgradeManagedServiceDtoValidator : AbstractValidator<UpgradeManagedServiceDto>
{
    public UpgradeManagedServiceDtoValidator()
    {
        RuleFor(x => x.ImageTag)
            .Must(ServiceValidation.IsValidTag)
            .WithMessage("Sürüm etiketi harf, rakam, nokta, alt çizgi ve tire içerebilir (en fazla 128 karakter).");
    }
}
