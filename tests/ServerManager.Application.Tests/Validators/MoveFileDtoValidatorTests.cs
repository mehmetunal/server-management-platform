using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Validators.Files;

namespace ServerManager.Application.Tests.Validators;

public class MoveFileDtoValidatorTests
{
    private readonly MoveFileDtoValidator _validator = new();

    [Fact]
    public void Accepts_rename()
    {
        Assert.True(_validator.Validate(new MoveFileDto { Source = "/srv/a.txt", Destination = "/srv/b.txt" }).IsValid);
    }

    [Theory]
    [InlineData("/srv/a.txt")]
    [InlineData("/srv/./a.txt")]
    [InlineData("//srv/a.txt")]
    public void Rejects_same_destination(string destination)
    {
        var result = _validator.Validate(new MoveFileDto { Source = "/srv/a.txt", Destination = destination });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(MoveFileDto.Destination));
    }
}
