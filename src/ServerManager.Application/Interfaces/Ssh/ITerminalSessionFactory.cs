using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Ssh;

public interface ITerminalSessionFactory
{
    Task<ServiceResult<ITerminalSession>> OpenAsync(
        TerminalOpenRequest request,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default);
}
