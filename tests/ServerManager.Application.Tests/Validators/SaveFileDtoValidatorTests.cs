using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Validators.Files;

namespace ServerManager.Application.Tests.Validators;

public class SaveFileDtoValidatorTests
{
    private readonly SaveFileDtoValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("LF")]
    [InlineData("CRLF")]
    public void Accepts_known_line_endings(string? lineEnding)
    {
        Assert.True(_validator.Validate(new SaveFileDto { Path = "/srv/a.txt", LineEnding = lineEnding }).IsValid);
    }

    [Fact]
    public void Rejects_unknown_line_ending()
    {
        var result = _validator.Validate(new SaveFileDto { Path = "/srv/a.txt", LineEnding = "CR" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveFileDto.LineEnding));
    }
}
