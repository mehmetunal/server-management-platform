using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Validators.Backups;

namespace ServerManager.Application.Tests.Validators;

public class BackupRestoreDtoValidatorTests
{
    private readonly BackupRestoreDtoValidator _validator = new();

    private static BackupRestoreDto Valid() => new()
    {
        RunId = Guid.NewGuid(),
        TargetServerId = Guid.NewGuid(),
        TargetDirectory = "/restore/test",
        Confirmed = true
    };

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Confirmation_is_required()
    {
        var dto = Valid();
        dto.Confirmed = false;

        Assert.Contains(_validator.Validate(dto).Errors, e => e.PropertyName == nameof(BackupRestoreDto.Confirmed));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("/proc")]
    [InlineData("/srv/../etc")]
    public void Unsafe_target_directory_fails(string directory)
    {
        var dto = Valid();
        dto.TargetDirectory = directory;

        Assert.Contains(_validator.Validate(dto).Errors, e => e.PropertyName == nameof(BackupRestoreDto.TargetDirectory));
    }

    [Fact]
    public void Invalid_target_names_fail()
    {
        var dto = Valid();
        dto.TargetVolume = "-x";
        dto.TargetContainer = "a b";
        dto.TargetDatabase = "--drop";

        var errors = _validator.Validate(dto).Errors.Select(e => e.PropertyName).ToList();

        Assert.Contains(nameof(BackupRestoreDto.TargetVolume), errors);
        Assert.Contains(nameof(BackupRestoreDto.TargetContainer), errors);
        Assert.Contains(nameof(BackupRestoreDto.TargetDatabase), errors);
    }
}
