using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.Validators.Docker;

namespace ServerManager.Application.Tests.Validators;

public class RenameContainerDtoValidatorTests
{
    private readonly RenameContainerDtoValidator _validator = new();

    [Fact]
    public void Accepts_valid_rename()
    {
        Assert.True(_validator.Validate(new RenameContainerDto { Container = "web", NewName = "web-2" }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("new name")]
    [InlineData("web$(id)")]
    public void Rejects_invalid_new_names(string newName)
    {
        var result = _validator.Validate(new RenameContainerDto { Container = "web", NewName = newName });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RenameContainerDto.NewName));
    }

    [Fact]
    public void Rejects_invalid_source_container()
    {
        var result = _validator.Validate(new RenameContainerDto { Container = "web;id", NewName = "web-2" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RenameContainerDto.Container));
    }
}
