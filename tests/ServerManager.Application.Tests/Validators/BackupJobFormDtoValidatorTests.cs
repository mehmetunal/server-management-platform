using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Validators.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Validators;

public class BackupJobFormDtoValidatorTests
{
    private readonly BackupJobFormDtoValidator _validator = new();

    private static BackupJobFormDto Valid() => new()
    {
        Name = "Nginx ayarları",
        ServerId = Guid.NewGuid(),
        StorageId = Guid.NewGuid(),
        SourceType = BackupSourceType.Files,
        Paths = "/etc/nginx\n/srv/app",
        EncryptionEnabled = true,
        Passphrase = "uzun-bir-parola-123",
        PassphraseConfirm = "uzun-bir-parola-123",
        ScheduleType = BackupScheduleType.Daily,
        ScheduleTime = "03:00",
        KeepLast = 7
    };

    private bool HasError(BackupJobFormDto dto, string property) =>
        _validator.Validate(dto).Errors.Any(e => e.PropertyName == property);

    [Fact]
    public void Valid_files_job_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("relative/path")]
    [InlineData("/proc/1")]
    [InlineData("/srv/../etc")]
    public void Unsafe_file_paths_fail(string path)
    {
        var dto = Valid();
        dto.Paths = path;

        Assert.True(HasError(dto, nameof(BackupJobFormDto.Paths)));
    }

    [Fact]
    public void Passphrase_is_required_only_without_a_stored_one()
    {
        var dto = Valid();
        dto.Passphrase = dto.PassphraseConfirm = null;
        Assert.True(HasError(dto, nameof(BackupJobFormDto.Passphrase)));

        dto.HasStoredPassphrase = true;
        Assert.False(HasError(dto, nameof(BackupJobFormDto.Passphrase)));

        dto.EncryptionEnabled = false;
        dto.HasStoredPassphrase = false;
        Assert.False(HasError(dto, nameof(BackupJobFormDto.Passphrase)));
    }

    [Fact]
    public void Short_or_mismatched_passphrase_fails()
    {
        var dto = Valid();
        dto.Passphrase = dto.PassphraseConfirm = "kisa";
        Assert.True(HasError(dto, nameof(BackupJobFormDto.Passphrase)));

        dto = Valid();
        dto.PassphraseConfirm = "baska-bir-parola-123";
        Assert.True(HasError(dto, nameof(BackupJobFormDto.Passphrase)));
    }

    [Theory]
    [InlineData("-volume")]
    [InlineData("vol ume")]
    [InlineData("vol;rm")]
    public void Invalid_volume_name_fails(string volume)
    {
        var dto = Valid();
        dto.SourceType = BackupSourceType.DockerVolume;
        dto.VolumeName = volume;

        Assert.True(HasError(dto, nameof(BackupJobFormDto.VolumeName)));
    }

    [Theory]
    [InlineData("a\nb", true)]
    [InlineData("a\rb", true)]
    [InlineData("a\0b", true)]
    [InlineData("p@ss w'rd\t$x", false)]
    public void Database_password_rejects_line_breaks_and_nul(string password, bool expectedError)
    {
        var dto = Valid();
        dto.SourceType = BackupSourceType.Database;
        dto.DatabaseEngine = BackupDatabaseEngine.PostgreSql;
        dto.DatabaseName = "shop";
        dto.DatabaseUser = "shop_user";
        dto.ContainerName = "app-db";
        dto.DatabasePassword = password;

        Assert.Equal(expectedError, HasError(dto, nameof(BackupJobFormDto.DatabasePassword)));
    }

    [Fact]
    public void Database_job_validates_names_and_password()
    {
        var dto = Valid();
        dto.SourceType = BackupSourceType.Database;
        dto.DatabaseEngine = BackupDatabaseEngine.PostgreSql;
        dto.DatabaseName = "shop";
        dto.DatabaseUser = "shop_user";
        dto.ContainerName = "app-db";
        Assert.True(_validator.Validate(dto).IsValid);

        dto.DatabaseName = "--help";
        dto.DatabaseUser = "-u";
        dto.DatabasePassword = "a\0b";
        dto.DatabasePort = 70000;
        Assert.True(HasError(dto, nameof(BackupJobFormDto.DatabaseName)));
        Assert.True(HasError(dto, nameof(BackupJobFormDto.DatabaseUser)));
        Assert.True(HasError(dto, nameof(BackupJobFormDto.DatabasePassword)));
        Assert.True(HasError(dto, nameof(BackupJobFormDto.DatabasePort)));
    }

    [Theory]
    [InlineData("3:00")]
    [InlineData("25:00")]
    [InlineData("abc")]
    public void Invalid_schedule_time_fails(string time)
    {
        var dto = Valid();
        dto.ScheduleTime = time;

        Assert.True(HasError(dto, nameof(BackupJobFormDto.ScheduleTime)));
    }

    [Fact]
    public void Hourly_interval_and_retention_limits_are_enforced()
    {
        var dto = Valid();
        dto.ScheduleType = BackupScheduleType.Hourly;
        dto.ScheduleIntervalHours = 0;
        dto.KeepLast = 0;
        dto.KeepDays = -1;

        Assert.True(HasError(dto, nameof(BackupJobFormDto.ScheduleIntervalHours)));
        Assert.True(HasError(dto, nameof(BackupJobFormDto.KeepLast)));
        Assert.True(HasError(dto, nameof(BackupJobFormDto.KeepDays)));
    }

    [Fact]
    public void Time_parser_returns_minute_of_day()
    {
        Assert.True(BackupJobFormDtoValidator.TryParseTime("23:45", out var minutes));
        Assert.Equal(23 * 60 + 45, minutes);
    }
}
