using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Tests.TestData;
using ServerManager.Application.Validators.Servers;

namespace ServerManager.Application.Tests.Validators;

public class UpdateServerDtoValidatorTests
{
    private readonly UpdateServerDtoValidator _validator = new();

    [Fact]
    public void Blank_secrets_are_allowed_on_update()
    {
        var result = _validator.Validate(ServerTestData.ValidUpdateDto(Guid.NewGuid()));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Empty_id_fails()
    {
        var result = _validator.Validate(ServerTestData.ValidUpdateDto(Guid.Empty));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateServerDto.Id));
    }
}
