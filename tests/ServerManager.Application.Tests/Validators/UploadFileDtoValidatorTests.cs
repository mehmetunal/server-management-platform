using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Validators.Files;

namespace ServerManager.Application.Tests.Validators;

public class UploadFileDtoValidatorTests
{
    private readonly UploadFileDtoValidator _validator = new();

    [Fact]
    public void Accepts_valid_upload()
    {
        Assert.True(_validator.Validate(new UploadFileDto { Directory = "/srv", FileName = "backup.tar.gz" }).IsValid);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..")]
    [InlineData("")]
    public void Rejects_names_that_escape_directory(string fileName)
    {
        var result = _validator.Validate(new UploadFileDto { Directory = "/srv", FileName = fileName });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadFileDto.FileName));
    }
}
