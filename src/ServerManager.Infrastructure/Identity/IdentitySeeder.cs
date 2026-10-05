using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;

namespace ServerManager.Infrastructure.Identity;

public class IdentitySeeder : IPermissionSeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SeedOptions _seedOptions;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<SeedOptions> seedOptions,
        ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _seedOptions = seedOptions.Value;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedAsync(DefaultRolePermissions.Matrix);
        await SeedAdminAsync();
    }

    public async Task SeedAsync(IReadOnlyDictionary<string, IReadOnlyList<string>> rolePermissions, CancellationToken cancellationToken = default)
    {
        foreach (var (roleName, permissions) in rolePermissions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                _logger.LogWarning("İzin eklenecek rol bulunamadı: {Role}", roleName);
                continue;
            }

            var existingPermissions = (await _roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permission in permissions.Where(p => !existingPermissions.Contains(p)))
            {
                EnsureSucceeded(
                    await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission)),
                    $"Yetki eklenemedi: {roleName} / {permission}");
                _logger.LogInformation("Yetki eklendi: {Role} -> {Permission}", roleName, permission);
            }
        }
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in Roles.All)
        {
            if (await _roleManager.FindByNameAsync(roleName) is not null)
                continue;

            var role = new ApplicationRole(roleName) { Description = Roles.Descriptions[roleName] };
            EnsureSucceeded(await _roleManager.CreateAsync(role), $"Rol oluşturulamadı: {roleName}");
            _logger.LogInformation("Rol oluşturuldu: {Role}", roleName);
        }
    }

    private async Task SeedAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_seedOptions.AdminEmail) || string.IsNullOrWhiteSpace(_seedOptions.AdminPassword))
        {
            if (_userManager.Users.Any())
                return;

            _logger.LogWarning(
                "Sistemde kullanıcı yok ve Seed:AdminEmail / Seed:AdminPassword tanımlı değil. İlk SuperAdmin oluşturulmadı.");
            return;
        }

        // Seed yalnızca ilk kurulum içindir. Bir SuperAdmin zaten varsa (e-postası değiştirilmiş olsa bile) yeni hesap açılmaz;
        // aksi halde yapılandırmada kalan parola ile her açılışta yeni bir SuperAdmin oluşurdu.
        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
        if (superAdmins.Count > 0)
        {
            _logger.LogWarning(
                "Seed:AdminEmail / Seed:AdminPassword hâlâ tanımlı ancak sistemde SuperAdmin var; seed atlandı. Bu değerleri yapılandırmadan kaldırın.");
            return;
        }

        if (await _userManager.FindByEmailAsync(_seedOptions.AdminEmail) is not null)
        {
            _logger.LogWarning(
                "Seed:AdminEmail ile kayıtlı bir kullanıcı var fakat SuperAdmin değil; yetkisi otomatik yükseltilmedi. Seed atlandı.");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = _seedOptions.AdminEmail,
            Email = _seedOptions.AdminEmail,
            EmailConfirmed = true,
            FullName = _seedOptions.AdminFullName ?? "Super Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        EnsureSucceeded(await _userManager.CreateAsync(admin, _seedOptions.AdminPassword), "İlk SuperAdmin oluşturulamadı");
        EnsureSucceeded(await _userManager.AddToRoleAsync(admin, Roles.SuperAdmin), "SuperAdmin rolü atanamadı");
        _logger.LogInformation("İlk SuperAdmin oluşturuldu: {Email}", admin.Email);
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"{message}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}
