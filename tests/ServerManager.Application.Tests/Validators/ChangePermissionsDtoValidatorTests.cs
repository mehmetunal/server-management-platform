using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Validators.Files;

namespace ServerManager.Application.Tests.Validators;

public class ChangePermissionsDtoValidatorTests
{
    private readonly ChangePermissionsDtoValidator _validator = new();

    [Theory]
    [InlineData("0755", null, null)]
    [InlineData("u+x", null, null)]
    [InlineData(null, "deploy", null)]
    [InlineData(null, null, "1000")]
    [InlineData("640", "www-data", "www-data")]
    public void Accepts_any_valid_change(string? mode, string? owner, string? group)
    {
        var dto = new ChangePermissionsDto { Path = "/srv/app", Mode = mode, Owner = owner, Group = group };

        Assert.True(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void Requires_at_least_one_change()
    {
        var result = _validator.Validate(new ChangePermissionsDto { Path = "/srv/app", Mode = " " });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ChangePermissionsDto.Mode));
    }

    [Theory]
    [InlineData("999", null, null, nameof(ChangePermissionsDto.Mode))]
    [InlineData(null, "root;id", null, nameof(ChangePermissionsDto.Owner))]
    [InlineData(null, null, "-R", nameof(ChangePermissionsDto.Group))]
    public void Rejects_invalid_values(string? mode, string? owner, string? group, string property)
    {
        var result = _validator.Validate(new ChangePermissionsDto { Path = "/srv/app", Mode = mode, Owner = owner, Group = group });

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Rejects_relative_path()
    {
        var result = _validator.Validate(new ChangePermissionsDto { Path = "srv/app", Mode = "0755" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ChangePermissionsDto.Path));
    }
}
