using FluentValidation;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;

namespace ServerManager.Application.Validators.Files;

public class ChangePermissionsDtoValidator : AbstractValidator<ChangePermissionsDto>
{
    public ChangePermissionsDtoValidator()
    {
        RuleFor(x => x.Path)
            .Must(path => RemotePath.Normalize(path) is not null).WithMessage(FileValidationMessages.InvalidPath);

        RuleFor(x => x.Mode)
            .Must(FileModes.IsValidMode)
            .When(x => !string.IsNullOrWhiteSpace(x.Mode))
            .WithMessage("Mod sekizli (ör. 0755) veya sembolik (ör. u+x,g-w) olmalıdır.");

        RuleFor(x => x.Owner)
            .Must(FileModes.IsValidPrincipal)
            .When(x => !string.IsNullOrWhiteSpace(x.Owner))
            .WithMessage("Geçersiz kullanıcı adı.");

        RuleFor(x => x.Group)
            .Must(FileModes.IsValidPrincipal)
            .When(x => !string.IsNullOrWhiteSpace(x.Group))
            .WithMessage("Geçersiz grup adı.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Mode) || !string.IsNullOrWhiteSpace(x.Owner) || !string.IsNullOrWhiteSpace(x.Group))
            .OverridePropertyName(nameof(ChangePermissionsDto.Mode))
            .WithMessage("Mod, sahip veya gruptan en az birini girin.");
    }
}
