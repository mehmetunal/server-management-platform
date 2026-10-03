using FluentValidation;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;

namespace ServerManager.Application.Validators.Files;

public class CopyFileDtoValidator : AbstractValidator<CopyFileDto>
{
    public CopyFileDtoValidator()
    {
        RuleFor(x => x.Source)
            .Must(path => RemotePath.Normalize(path) is not null).WithMessage(FileValidationMessages.InvalidPath);

        RuleFor(x => x.Destination)
            .Must(path => RemotePath.Normalize(path) is not null).WithMessage(FileValidationMessages.InvalidPath)
            .Must((dto, destination) => !IsSameOrInside(dto.Source, destination))
            .WithMessage("Hedef, kaynağın kendisi veya içindeki bir yol olamaz.");
    }

    private static bool IsSameOrInside(string source, string destination)
    {
        var normalizedSource = RemotePath.Normalize(source);
        var normalizedDestination = RemotePath.Normalize(destination);
        return normalizedSource is not null
               && normalizedDestination is not null
               && RemotePath.IsSameOrDescendant(normalizedDestination, normalizedSource);
    }
}
