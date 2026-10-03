using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;

namespace ServerManager.Infrastructure.Identity;

public class IdentitySeeder
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
        await SeedAdminAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in Roles.All)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ApplicationRole(roleName) { Description = Roles.Descriptions[roleName] };
                EnsureSucceeded(await _roleManager.CreateAsync(role), $"Rol oluşturulamadı: {roleName}");
                _logger.LogInformation("Rol oluşturuldu: {Role}", roleName);
            }

            var existingPermissions = (await _roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permission in DefaultRolePermissions.Matrix[roleName].Where(p => !existingPermissions.Contains(p)))
            {
                EnsureSucceeded(
                    await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission)),
                    $"Yetki eklenemedi: {roleName} / {permission}");
                _logger.LogInformation("Yetki eklendi: {Role} -> {Permission}", roleName, permission);
            }
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

        var existing = await _userManager.FindByEmailAsync(_seedOptions.AdminEmail);
        if (existing is not null)
            return;

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
