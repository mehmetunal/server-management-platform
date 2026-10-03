using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Validators.Files;

namespace ServerManager.Application.Tests.Validators;

public class CopyFileDtoValidatorTests
{
    private readonly CopyFileDtoValidator _validator = new();

    [Theory]
    [InlineData("/srv/app", "/srv/app-backup")]
    [InlineData("/srv/app/config.json", "/srv/config.json")]
    public void Accepts_destination_outside_source(string source, string destination)
    {
        Assert.True(_validator.Validate(new CopyFileDto { Source = source, Destination = destination }).IsValid);
    }

    [Theory]
    [InlineData("/srv/app", "/srv/app")]
    [InlineData("/srv/app", "/srv/app/")]
    [InlineData("/srv/app", "/srv/app/nested/copy")]
    [InlineData("/srv/app", "/srv/x/../app/copy")]
    public void Rejects_destination_inside_source(string source, string destination)
    {
        var result = _validator.Validate(new CopyFileDto { Source = source, Destination = destination });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CopyFileDto.Destination));
    }

    [Fact]
    public void Rejects_relative_paths()
    {
        var result = _validator.Validate(new CopyFileDto { Source = "app", Destination = "copy" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CopyFileDto.Source));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CopyFileDto.Destination));
    }
}
