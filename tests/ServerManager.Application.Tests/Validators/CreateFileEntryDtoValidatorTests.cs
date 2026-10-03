using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Validators.Files;

namespace ServerManager.Application.Tests.Validators;

public class CreateFileEntryDtoValidatorTests
{
    private readonly CreateFileEntryDtoValidator _validator = new();

    [Fact]
    public void Accepts_valid_entry()
    {
        Assert.True(_validator.Validate(new CreateFileEntryDto { Directory = "/srv", Name = ".env" }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    public void Rejects_invalid_names(string name)
    {
        var result = _validator.Validate(new CreateFileEntryDto { Directory = "/srv", Name = name });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateFileEntryDto.Name));
    }

    [Fact]
    public void Rejects_relative_directory()
    {
        var result = _validator.Validate(new CreateFileEntryDto { Directory = "srv", Name = "a" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateFileEntryDto.Directory));
    }
}
