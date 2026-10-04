using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class PanelSettingRepository : IPanelSettingRepository
{
    private readonly ApplicationDbContext _context;

    public PanelSettingRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PanelSetting>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.PanelSettings.AsNoTracking().ToListAsync(cancellationToken);

    public async Task UpsertAsync(IReadOnlyList<PanelSetting> settings, CancellationToken cancellationToken = default)
    {
        var keys = settings.Select(s => s.Key).ToArray();
        var existing = await _context.PanelSettings.Where(s => keys.Contains(s.Key)).ToListAsync(cancellationToken);
        foreach (var setting in settings)
        {
            var row = existing.FirstOrDefault(s => s.Key == setting.Key);
            if (row is null)
            {
                _context.PanelSettings.Add(setting);
                continue;
            }

            row.Value = setting.Value;
            row.UpdatedAt = setting.UpdatedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
