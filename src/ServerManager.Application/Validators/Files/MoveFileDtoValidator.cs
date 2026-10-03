using FluentValidation;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;

namespace ServerManager.Application.Validators.Files;

public class MoveFileDtoValidator : AbstractValidator<MoveFileDto>
{
    public MoveFileDtoValidator()
    {
        RuleFor(x => x.Source)
            .Must(path => RemotePath.Normalize(path) is not null).WithMessage(FileValidationMessages.InvalidPath);

        RuleFor(x => x.Destination)
            .Must(path => RemotePath.Normalize(path) is not null).WithMessage(FileValidationMessages.InvalidPath)
            .Must((dto, destination) => RemotePath.Normalize(destination) != RemotePath.Normalize(dto.Source))
            .WithMessage("Hedef, kaynakla aynı olamaz.");
    }
}
