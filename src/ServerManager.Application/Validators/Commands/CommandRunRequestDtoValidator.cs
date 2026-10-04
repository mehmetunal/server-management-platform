using FluentValidation;
using ServerManager.Application.Commands;
using ServerManager.Application.DTOs.Commands;

namespace ServerManager.Application.Validators.Commands;

public sealed class CommandRunRequestDtoValidator : AbstractValidator<CommandRunRequestDto>
{
    public CommandRunRequestDtoValidator()
    {
        RuleFor(x => x.Command)
            .NotEmpty().WithMessage("Çalıştırılacak komutu yazın.")
            .MaximumLength(CommandRunRules.MaxCommandLength).WithMessage($"Komut en fazla {CommandRunRules.MaxCommandLength} karakter olabilir.");

        RuleFor(x => x.ServerIds)
            .NotEmpty().WithMessage("En az bir sunucu seçin.")
            .Must(ids => ids.Count <= CommandRunRules.MaxServers).WithMessage($"Tek seferde en fazla {CommandRunRules.MaxServers} sunucu seçilebilir.");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(CommandRunRules.MinTimeoutSeconds, CommandRunRules.MaxTimeoutSeconds)
            .WithMessage($"Zaman aşımı {CommandRunRules.MinTimeoutSeconds} ile {CommandRunRules.MaxTimeoutSeconds} saniye arasında olmalıdır.");
    }
}
