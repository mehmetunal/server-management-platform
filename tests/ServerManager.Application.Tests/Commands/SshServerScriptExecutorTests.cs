using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Infrastructure.Commands;

namespace ServerManager.Application.Tests.Commands;

public class SshServerScriptExecutorTests
{
    private readonly IServerConnectionProvider _connections = Substitute.For<IServerConnectionProvider>();
    private readonly IRemoteCommandRunner _runner = Substitute.For<IRemoteCommandRunner>();
    private readonly Guid _serverId = Guid.NewGuid();

    [Fact]
    public void BuildCommand_QuotesScriptAndMergesStderr()
    {
        var command = SshServerScriptExecutor.BuildCommand("echo 'a b'\r\nid -un");

        Assert.Equal("sh -c 'echo '\"'\"'a b'\"'\"'\nid -un\n' 2>&1", command);
    }

    [Fact]
    public async Task ExecuteAsync_RefusesElevationWhenServerHasNoSudo()
    {
        SetupConnection(useSudo: false);
        var executor = new SshServerScriptExecutor(_connections, _runner);

        var result = await executor.ExecuteAsync(_serverId, "id", elevate: true, TimeSpan.FromSeconds(10));

        Assert.False(result.Executed);
        Assert.Contains("sudo", result.ErrorMessage);
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<RemoteCommandOutput>(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsConnectionFailureWithoutRunning()
    {
        _connections.GetAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Failure("Host key doğrulanmamış."));
        var executor = new SshServerScriptExecutor(_connections, _runner);

        var result = await executor.ExecuteAsync(_serverId, "id", elevate: false, TimeSpan.FromSeconds(10));

        Assert.False(result.Executed);
        Assert.Equal("Host key doğrulanmamış.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_RunsElevatedCommandAndReturnsOutput()
    {
        SetupConnection(useSudo: true);
        RemoteCommand? sent = null;
        var remote = Substitute.For<IRemoteCommandExecutor>();
        remote.ExecuteAsync(Arg.Do<RemoteCommand>(c => sent = c), Arg.Any<CancellationToken>())
            .Returns(new RemoteCommandOutput { ExitCode = 3, Stdout = "hata\n" });
        _runner.RunAsync(Arg.Any<RemoteExecutionContext>(), Arg.Any<Func<IRemoteCommandExecutor, CancellationToken, Task<ServiceResult<RemoteCommandOutput>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<IRemoteCommandExecutor, CancellationToken, Task<ServiceResult<RemoteCommandOutput>>>>()(remote, CancellationToken.None));
        var executor = new SshServerScriptExecutor(_connections, _runner);

        var result = await executor.ExecuteAsync(_serverId, "exit 3", elevate: true, TimeSpan.FromSeconds(10));

        Assert.True(result.Executed);
        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("hata\n", result.Output);
        Assert.NotNull(sent);
        Assert.True(sent!.Elevate);
        Assert.Equal(TimeSpan.FromSeconds(10), sent.Timeout);
    }

    private void SetupConnection(bool useSudo) =>
        _connections.GetAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection
            {
                ServerId = _serverId,
                ServerName = "web-1",
                Context = new RemoteExecutionContext
                {
                    Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "deploy" },
                    UseSudo = useSudo
                }
            }));
}
