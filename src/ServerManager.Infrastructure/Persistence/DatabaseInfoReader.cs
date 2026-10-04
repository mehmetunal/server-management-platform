using Microsoft.EntityFrameworkCore;
using ServerManager.Application.DTOs.Settings;
using ServerManager.Application.Interfaces;

namespace ServerManager.Infrastructure.Persistence;

public sealed class DatabaseInfoReader : IDatabaseInfoReader
{
    private readonly ApplicationDbContext _context;

    public DatabaseInfoReader(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DatabaseStatusDto> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _context.Database.CanConnectAsync(cancellationToken))
                return new DatabaseStatusDto(false, null, "Veritabanına bağlanılamadı.");

            var version = await _context.Database
                .SqlQueryRaw<long?>("SELECT CAST(MAX([Version]) AS bigint) AS [Value] FROM [VersionInfo]")
                .FirstOrDefaultAsync(cancellationToken);

            return new DatabaseStatusDto(true, version, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DatabaseStatusDto(false, null, "Veritabanı durumu okunamadı.");
        }
    }
}
