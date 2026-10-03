using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Ssh;

public interface ISshConnectionTester
{
    Task<SshConnectionTestResult> TestAsync(SshConnectionRequest request, CancellationToken cancellationToken = default);
}
