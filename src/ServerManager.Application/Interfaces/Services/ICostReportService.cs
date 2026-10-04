using ServerManager.Application.DTOs.Costs;

namespace ServerManager.Application.Interfaces.Services;

public interface ICostReportService
{
    Task<CostReportDto> GetReportAsync(CancellationToken cancellationToken = default);
}
