using FluentValidation;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;

namespace ServerManager.Application.Validators.Files;

public class UploadFileDtoValidator : AbstractValidator<UploadFileDto>
{
    public UploadFileDtoValidator()
    {
        RuleFor(x => x.Directory)
            .Must(path => RemotePath.Normalize(path) is not null).WithMessage(FileValidationMessages.InvalidPath);

        RuleFor(x => x.FileName)
            .Must(RemotePath.IsValidName).WithMessage(FileValidationMessages.InvalidName);
    }
}
