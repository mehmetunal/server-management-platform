using FluentValidation;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;

namespace ServerManager.Application.Validators.Files;

public class SaveFileDtoValidator : AbstractValidator<SaveFileDto>
{
    public SaveFileDtoValidator()
    {
        RuleFor(x => x.Path)
            .Must(path => RemotePath.Normalize(path) is not null).WithMessage(FileValidationMessages.InvalidPath);

        RuleFor(x => x.LineEnding)
            .Must(value => value is null or TextFileCodec.Lf or TextFileCodec.Crlf)
            .WithMessage("Geçersiz satır sonu.");
    }
}
