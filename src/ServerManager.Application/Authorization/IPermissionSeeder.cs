namespace ServerManager.Application.Authorization;

public interface IPermissionSeeder
{
    /// <summary>Rollerde eksik olan izinleri ekler; var olan izinleri kaldırmaz.</summary>
    Task SeedAsync(IReadOnlyDictionary<string, IReadOnlyList<string>> rolePermissions, CancellationToken cancellationToken = default);
}
