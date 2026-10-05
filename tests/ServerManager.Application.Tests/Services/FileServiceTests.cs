using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Files;
using ServerManager.Application.Interfaces.Files;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Application.Validators.Files;

namespace ServerManager.Application.Tests.Services;

public class FileServiceTests
{
    private const string ServerName = "web-01";

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IRemoteFileSystem _fileSystem = Substitute.For<IRemoteFileSystem>();
    private readonly IRemoteFileSession _session = Substitute.For<IRemoteFileSession>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly RemoteExecutionContext _context = new() { Connection = new SshConnectionRequest { Host = "10.0.0.5", Username = "deploy" } };
    private readonly FileService _service;

    public FileServiceTests()
    {
        _connectionProvider.GetAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _serverId, ServerName = ServerName, Context = _context }));

        _session.HomeDirectory.Returns("/home/deploy");
        _fileSystem.ResolveAsync(_context, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ServiceResult<RemotePathResolution>.Success(new RemotePathResolution(false, call.ArgAt<string>(1), call.ArgAt<string>(1))));
        UseSession<bool>();
        UseSession<FileListingDto>();
        UseSession<FileContentDto>();
        UseSession<FileSaveResultDto>();

        _service = new FileService(
            _connectionProvider,
            _fileSystem,
            _auditLog,
            new SaveFileDtoValidator(),
            new CreateFileEntryDtoValidator(),
            new MoveFileDtoValidator(),
            new CopyFileDtoValidator(),
            new ChangePermissionsDtoValidator(),
            new UploadFileDtoValidator(),
            Options.Create(new FileManagerOptions { MaxEditKilobytes = 1, MaxUploadMegabytes = 1 }),
            NullLogger<FileService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void UseSession<T>() =>
        _fileSystem.RunAsync(_context, Arg.Any<Func<IRemoteFileSession, CancellationToken, Task<ServiceResult<T>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<IRemoteFileSession, CancellationToken, Task<ServiceResult<T>>>>()(_session, CancellationToken.None));

    private void GivenEntry(string path, RemoteFileKind kind, int mode = 0b110_100_100, int userId = 1000, int groupId = 100) =>
        _session.GetInfoAsync(path, Arg.Any<CancellationToken>())
            .Returns(new RemoteFileInfo(RemotePath.GetFileName(path), path, kind, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), mode, userId, groupId));

    private void GivenResolution(string path, bool isLink, string target, string? entry = null) =>
        _fileSystem.ResolveAsync(_context, path, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<RemotePathResolution>.Success(new RemotePathResolution(isLink, target, entry ?? path)));

    private void GivenContent(string path, string content) =>
        _session.ReadAsync(path, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes(content));

    private Task AssertAuditedAsync(string action, bool isSuccess, string detailsFragment) =>
        _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == action
                                    && e.IsSuccess == isSuccess
                                    && e.EntityId == _serverId.ToString()
                                    && e.TargetName == ServerName
                                    && e.Details != null && e.Details.Contains(detailsFragment)),
            Arg.Any<CancellationToken>());

    [Fact]
    public async Task List_defaults_to_home_sorts_directories_first_and_resolves_owner_names()
    {
        GivenEntry("/home/deploy", RemoteFileKind.Directory);
        _session.ListAsync("/home/deploy", Arg.Any<CancellationToken>()).Returns(new[]
        {
            new RemoteFileInfo("b.txt", "/home/deploy/b.txt", RemoteFileKind.File, 1, DateTime.UtcNow, 0b110_100_100, 1000, 100),
            new RemoteFileInfo("logs", "/home/deploy/logs", RemoteFileKind.Directory, 0, DateTime.UtcNow, 0b111_101_101, 0, 0),
            new RemoteFileInfo("A.txt", "/home/deploy/A.txt", RemoteFileKind.File, 1, DateTime.UtcNow, 0b110_000_000, 4242, 100)
        });
        GivenContent("/etc/passwd", "root:x:0:0::/root:/bin/sh\ndeploy:x:1000:100::/home/deploy:/bin/sh\n");
        _session.ReadAsync("/etc/group", Arg.Any<int>(), Arg.Any<CancellationToken>()).ThrowsAsync(new RemoteFileException(RemoteFileErrorKind.PermissionDenied));

        var result = await _service.ListAsync(_serverId, null, Ct);

        Assert.True(result.IsSuccess);
        var listing = result.Data!;
        Assert.Equal("/home/deploy", listing.Path);
        Assert.Equal("/home", listing.ParentPath);
        Assert.Equal(new[] { "logs", "A.txt", "b.txt" }, listing.Entries.Select(e => e.Name));
        Assert.Equal("root", listing.Entries[0].Owner);
        Assert.Equal("4242", listing.Entries[1].Owner);
        Assert.Equal("100", listing.Entries[2].Group);
        Assert.Equal("rwxr-xr-x", listing.Entries[0].Permissions);
        Assert.Equal("0600", listing.Entries[1].OctalMode);
    }

    [Fact]
    public async Task List_rejects_file_path()
    {
        GivenEntry("/srv/a.txt", RemoteFileKind.File);

        var result = await _service.ListAsync(_serverId, "/srv/a.txt", Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Invalid_path_fails_before_connecting()
    {
        var result = await _service.ListAsync(_serverId, "relative/path", Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _connectionProvider.DidNotReceiveWithAnyArgs().GetAsync(default, Ct);
    }

    [Fact]
    public async Task Read_returns_text_with_version_and_audits()
    {
        GivenEntry("/srv/app.json", RemoteFileKind.File);
        GivenContent("/srv/app.json", "{\r\n}\r\n");

        var result = await _service.ReadAsync(_serverId, "/srv/app.json", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("CRLF", result.Data!.LineEnding);
        Assert.Equal(32, result.Data.Version.Length);
        await AssertAuditedAsync(AuditActions.FileRead, true, "Yol: /srv/app.json");
    }

    [Fact]
    public async Task Read_rejects_binary_and_oversized_files()
    {
        GivenEntry("/srv/app.bin", RemoteFileKind.File);
        _session.ReadAsync("/srv/app.bin", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 0x00, 0x01 });
        GivenEntry("/srv/big.log", RemoteFileKind.File);
        GivenContent("/srv/big.log", new string('a', 1025));

        var binary = await _service.ReadAsync(_serverId, "/srv/app.bin", Ct);
        var oversized = await _service.ReadAsync(_serverId, "/srv/big.log", Ct);

        Assert.Equal(ServiceErrorType.Validation, binary.ErrorType);
        Assert.Equal(ServiceErrorType.Validation, oversized.ErrorType);
        await _auditLog.DidNotReceiveWithAnyArgs().LogAsync(default!, Ct);
    }

    [Fact]
    public async Task Save_with_stale_version_returns_conflict_without_writing()
    {
        GivenEntry("/srv/app.json", RemoteFileKind.File);
        GivenContent("/srv/app.json", "changed on server");

        var result = await _service.SaveAsync(_serverId, new SaveFileDto { Path = "/srv/app.json", Content = "mine", Version = "STALE" }, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _session.DidNotReceiveWithAnyArgs().WriteAsync(default!, default!, default, Ct);
        await AssertAuditedAsync(AuditActions.FileEdit, false, "Hata: Dosya siz düzenlerken");
    }

    [Fact]
    public async Task Forced_save_writes_with_preserved_line_endings()
    {
        GivenEntry("/srv/app.json", RemoteFileKind.File);

        var result = await _service.SaveAsync(_serverId, new SaveFileDto { Path = "/srv/app.json", Content = "a\nb", LineEnding = "CRLF", Version = "STALE", Force = true }, Ct);

        Assert.True(result.IsSuccess);
        await _session.Received(1).WriteAsync("/srv/app.json", Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "a\r\nb"), false, Arg.Any<CancellationToken>());
        await AssertAuditedAsync(AuditActions.FileEdit, true, "üzerine yazıldı");
    }

    [Fact]
    public async Task Create_fails_when_entry_exists()
    {
        GivenEntry("/srv/new", RemoteFileKind.Directory);

        var result = await _service.CreateAsync(_serverId, new CreateFileEntryDto { Directory = "/srv", Name = "new", IsDirectory = true }, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _session.DidNotReceiveWithAnyArgs().CreateDirectoryAsync(default!, Ct);
    }

    [Fact]
    public async Task Create_directory_audits_directory_action()
    {
        var result = await _service.CreateAsync(_serverId, new CreateFileEntryDto { Directory = "/srv", Name = " new ", IsDirectory = true }, Ct);

        Assert.True(result.IsSuccess);
        await _session.Received(1).CreateDirectoryAsync("/srv/new", Arg.Any<CancellationToken>());
        await AssertAuditedAsync(AuditActions.FileDirectoryCreate, true, "Yol: /srv/new");
    }

    [Fact]
    public async Task Move_rejects_existing_destination()
    {
        GivenEntry("/srv/a.txt", RemoteFileKind.File);
        GivenEntry("/srv/b.txt", RemoteFileKind.File);

        var result = await _service.MoveAsync(_serverId, new MoveFileDto { Source = "/srv/a.txt", Destination = "/srv/b.txt" }, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _session.DidNotReceiveWithAnyArgs().RenameAsync(default!, default!, Ct);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/etc")]
    [InlineData("/var/")]
    public async Task Protected_paths_cannot_be_deleted_moved_or_chmodded(string path)
    {
        var delete = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = path, ConfirmationName = RemotePath.GetFileName(path.TrimEnd('/')) }, Ct);
        var move = await _service.MoveAsync(_serverId, new MoveFileDto { Source = path, Destination = "/srv/moved" }, Ct);
        var chmod = await _service.ChangePermissionsAsync(_serverId, new ChangePermissionsDto { Path = path, Mode = "0777" }, Ct);

        Assert.All(new[] { delete, move, chmod }, r => Assert.Equal(ServiceErrorType.Forbidden, r.ErrorType));
        await _connectionProvider.DidNotReceiveWithAnyArgs().GetAsync(default, Ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("App")]
    [InlineData("/srv/app")]
    public async Task Directory_delete_requires_exact_name(string? confirmation)
    {
        GivenEntry("/srv/app", RemoteFileKind.Directory);

        var result = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = "/srv/app", ConfirmationName = confirmation }, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _fileSystem.DidNotReceiveWithAnyArgs().DeleteRecursiveAsync(default!, default!, Ct);
        await AssertAuditedAsync(AuditActions.FileDelete, false, "Hata: Klasörü silmek için");
    }

    [Fact]
    public async Task Confirmed_directory_delete_removes_recursively()
    {
        GivenEntry("/srv/app", RemoteFileKind.Directory);
        _fileSystem.DeleteRecursiveAsync(_context, "/srv/app", Arg.Any<CancellationToken>()).Returns(ServiceResult.Success());

        var result = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = "/srv/app", ConfirmationName = " app " }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Klasör içeriğiyle birlikte silindi.", result.Message);
        await _session.DidNotReceiveWithAnyArgs().DeleteFileAsync(default!, Ct);
        await AssertAuditedAsync(AuditActions.FileDelete, true, "(klasör, içeriğiyle)");
    }

    [Fact]
    public async Task File_delete_does_not_need_confirmation()
    {
        GivenEntry("/srv/a.txt", RemoteFileKind.File);

        var result = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = "/srv/a.txt" }, Ct);

        Assert.True(result.IsSuccess);
        await _session.Received(1).DeleteFileAsync("/srv/a.txt", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Change_permissions_applies_mode_then_owner_and_audits_details()
    {
        _fileSystem.ChangeModeAsync(_context, "/srv/app", "0750", true, Arg.Any<CancellationToken>()).Returns(ServiceResult.Success());
        _fileSystem.ChangeOwnerAsync(_context, "/srv/app", "deploy", null, true, Arg.Any<CancellationToken>()).Returns(ServiceResult.Success());

        var result = await _service.ChangePermissionsAsync(_serverId, new ChangePermissionsDto { Path = "/srv/app", Mode = "0750", Owner = "deploy", Group = " ", Recursive = true }, Ct);

        Assert.True(result.IsSuccess);
        await AssertAuditedAsync(AuditActions.FilePermissions, true, "mod: 0750, sahip: deploy:-, alt öğeler dahil");
    }

    [Fact]
    public async Task Chmod_on_a_link_to_a_protected_path_is_refused()
    {
        GivenResolution("/home/deploy/root", isLink: true, target: "/etc");

        var result = await _service.ChangePermissionsAsync(_serverId, new ChangePermissionsDto { Path = "/home/deploy/root", Mode = "0777" }, Ct);

        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        await _fileSystem.DidNotReceiveWithAnyArgs().ChangeModeAsync(default!, default!, default!, default, Ct);
    }

    [Fact]
    public async Task Chmod_runs_on_the_resolved_target()
    {
        GivenResolution("/srv/current", isLink: true, target: "/srv/releases/v2");
        _fileSystem.ChangeModeAsync(_context, "/srv/releases/v2", "0750", false, Arg.Any<CancellationToken>()).Returns(ServiceResult.Success());

        var result = await _service.ChangePermissionsAsync(_serverId, new ChangePermissionsDto { Path = "/srv/current", Mode = "0750" }, Ct);

        Assert.True(result.IsSuccess);
        await AssertAuditedAsync(AuditActions.FilePermissions, true, "/srv/current → /srv/releases/v2");
    }

    [Fact]
    public async Task Recursive_chmod_on_a_symbolic_link_is_refused()
    {
        GivenResolution("/srv/current", isLink: true, target: "/srv/releases/v2");

        var result = await _service.ChangePermissionsAsync(_serverId, new ChangePermissionsDto { Path = "/srv/current", Mode = "0750", Recursive = true }, Ct);

        Assert.False(result.IsSuccess);
        await _fileSystem.DidNotReceiveWithAnyArgs().ChangeModeAsync(default!, default!, default!, default, Ct);
    }

    [Theory]
    [InlineData("/usr/bin")]
    [InlineData("/etc/ssh")]
    public async Task Recursive_operations_inside_system_directories_are_refused(string path)
    {
        GivenEntry(path, RemoteFileKind.Directory);

        var chmod = await _service.ChangePermissionsAsync(_serverId, new ChangePermissionsDto { Path = path, Mode = "0777", Recursive = true }, Ct);
        var delete = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = path, ConfirmationName = RemotePath.GetFileName(path) }, Ct);

        Assert.Equal(ServiceErrorType.Forbidden, chmod.ErrorType);
        Assert.Equal(ServiceErrorType.Forbidden, delete.ErrorType);
        await _fileSystem.DidNotReceiveWithAnyArgs().ChangeModeAsync(default!, default!, default!, default, Ct);
        await _fileSystem.DidNotReceiveWithAnyArgs().DeleteRecursiveAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Delete_through_a_linked_parent_checks_and_uses_the_real_path()
    {
        GivenResolution("/home/deploy/sys/etc", isLink: false, target: "/etc", entry: "/etc");

        var result = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = "/home/deploy/sys/etc", ConfirmationName = "etc" }, Ct);

        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        await _fileSystem.DidNotReceiveWithAnyArgs().DeleteRecursiveAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Deleting_a_link_to_a_directory_removes_only_the_link()
    {
        GivenResolution("/srv/current", isLink: true, target: "/srv/releases/v2");
        GivenEntry("/srv/current", RemoteFileKind.Directory);

        var result = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = "/srv/current" }, Ct);

        Assert.True(result.IsSuccess);
        await _session.Received(1).DeleteFileAsync("/srv/current", Arg.Any<CancellationToken>());
        await _fileSystem.DidNotReceiveWithAnyArgs().DeleteRecursiveAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Failed_chmod_skips_chown()
    {
        _fileSystem.ChangeModeAsync(_context, "/srv/app", "0750", false, Arg.Any<CancellationToken>()).Returns(ServiceResult.Failure("Operation not permitted"));

        var result = await _service.ChangePermissionsAsync(_serverId, new ChangePermissionsDto { Path = "/srv/app", Mode = "0750", Owner = "root" }, Ct);

        Assert.False(result.IsSuccess);
        await _fileSystem.DidNotReceiveWithAnyArgs().ChangeOwnerAsync(default!, default!, default, default, default, Ct);
    }

    [Fact]
    public async Task Upload_over_existing_file_requires_overwrite_and_is_not_audited_until_confirmed()
    {
        GivenEntry("/srv", RemoteFileKind.Directory);
        GivenEntry("/srv/a.txt", RemoteFileKind.File);
        using var content = new MemoryStream([1, 2, 3]);

        var conflict = await _service.UploadAsync(_serverId, new UploadFileDto { Directory = "/srv", FileName = "a.txt" }, content, 3, Ct);

        Assert.Equal(ServiceErrorType.Conflict, conflict.ErrorType);
        await _session.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, Ct);
        await _auditLog.DidNotReceiveWithAnyArgs().LogAsync(default!, Ct);

        var overwritten = await _service.UploadAsync(_serverId, new UploadFileDto { Directory = "/srv", FileName = "a.txt", Overwrite = true }, content, 3, Ct);

        Assert.True(overwritten.IsSuccess);
        await _session.Received(1).UploadAsync(content, "/srv/a.txt", Arg.Any<CancellationToken>());
        await AssertAuditedAsync(AuditActions.FileUpload, true, "üzerine yazıldı");
    }

    [Fact]
    public async Task Upload_rejects_oversized_file_before_connecting()
    {
        using var content = new MemoryStream();

        var result = await _service.UploadAsync(_serverId, new UploadFileDto { Directory = "/srv", FileName = "big.iso" }, content, 1024L * 1024 + 1, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _connectionProvider.DidNotReceiveWithAnyArgs().GetAsync(default, Ct);
    }

    [Fact]
    public async Task Remote_permission_error_is_translated()
    {
        GivenEntry("/srv/a.txt", RemoteFileKind.File);
        _session.DeleteFileAsync("/srv/a.txt", Arg.Any<CancellationToken>()).ThrowsAsync(new RemoteFileException(RemoteFileErrorKind.PermissionDenied));

        var result = await _service.DeleteAsync(_serverId, new DeleteFileDto { Path = "/srv/a.txt" }, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("Permission denied", result.Message);
        await AssertAuditedAsync(AuditActions.FileDelete, false, "Hata: Sunucuda bu işlem için yetki yok");
    }
}
