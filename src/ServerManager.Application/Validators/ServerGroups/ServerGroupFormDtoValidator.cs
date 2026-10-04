using FluentValidation;
using ServerManager.Application.DTOs.ServerGroups;

namespace ServerManager.Application.Validators.ServerGroups;

public sealed class ServerGroupFormDtoValidator : AbstractValidator<ServerGroupFormDto>
{
    public const int MaxServers = 500;

    public ServerGroupFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Grup adı zorunludur.")
            .MaximumLength(64).WithMessage("Grup adı en fazla 64 karakter olabilir.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Açıklama en fazla 500 karakter olabilir.");

        RuleFor(x => x.Color)
            .Must(ServerGroupColors.IsValid).WithMessage("Geçerli bir renk seçin.");

        RuleFor(x => x.ServerIds)
            .Must(ids => ids.Count <= MaxServers).WithMessage("Bir gruba en fazla 500 sunucu atanabilir.");
    }
}
