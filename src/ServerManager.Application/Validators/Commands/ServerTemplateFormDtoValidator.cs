using FluentValidation;
using ServerManager.Application.DTOs.Templates;

namespace ServerManager.Application.Validators.Commands;

public sealed class ServerTemplateFormDtoValidator : AbstractValidator<ServerTemplateFormDto>
{
    public const int MaxContentLength = 64_000;

    public ServerTemplateFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Şablon adı zorunludur.")
            .MaximumLength(128).WithMessage("Şablon adı en fazla 128 karakter olabilir.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Açıklama en fazla 500 karakter olabilir.");

        RuleFor(x => x.Kind)
            .IsInEnum().WithMessage("Şablon türünü seçin.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Şablon içeriği zorunludur.")
            .MaximumLength(MaxContentLength).WithMessage("Şablon içeriği en fazla 64.000 karakter olabilir.");

        RuleFor(x => x.Content)
            .Must(c => c.TrimStart().StartsWith("#cloud-config", StringComparison.Ordinal) || c.TrimStart().StartsWith("#!", StringComparison.Ordinal))
            .When(x => x.Kind == Domain.Enums.ServerTemplateKind.CloudInit && !string.IsNullOrWhiteSpace(x.Content))
            .WithMessage("cloud-init içeriği '#cloud-config' veya '#!' ile başlamalıdır.");
    }
}
