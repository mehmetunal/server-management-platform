using FluentValidation;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Application.Validators.Servers;

public sealed class UpdateServerDtoValidator : ServerFormDtoValidator<UpdateServerDto>
{
    public UpdateServerDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Sunucu kimliği zorunludur.");
    }
}
