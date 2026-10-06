using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Authorization;
using ServerManager.Infrastructure.Identity;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>Oturum claim'leri her istekte yeniden doğrulanır (security stamp aralığı 0); rol değişikliğinin etkisi hemen görülür.</summary>
public sealed class ImmediateStampWebFactory : ServerManagerWebFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero));
    }
}

[Collection(WebCollection.Name)]
public sealed class RoleManagementTests(ServerManagerWebFactory factory, ImmediateStampWebFactory immediate) : IClassFixture<ImmediateStampWebFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Roles_page_requires_roles_manage()
    {
        using var viewer = factory.CreateTestClient();
        await viewer.LoginAsync(await factory.CreateUserAsync(Roles.Viewer), ServerManagerWebFactory.DefaultUserPassword, Ct);
        using var admin = factory.CreateTestClient();
        await admin.LoginAsync(await factory.CreateUserAsync(Roles.Admin), ServerManagerWebFactory.DefaultUserPassword, Ct);

        using var denied = await viewer.GetAsync("/Roles", Ct);
        using var allowed = await admin.GetAsync("/Roles", Ct);

        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", denied.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Create_role_requires_antiforgery_and_persists_permissions()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var name = "Ops " + Guid.NewGuid().ToString("N")[..6];

        using var rejected = await client.SendAsync(Post("/Roles/Create", null, Form(name, Permissions.ServerView, Permissions.DockerView)), Ct);
        var token = await client.GetAntiforgeryTokenAsync("/Roles/Create", Ct);
        using var created = await client.SendAsync(Post("/Roles/Create", token, Form(name, Permissions.ServerView, Permissions.DockerView)), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal([Permissions.DockerView, Permissions.ServerView], await RolePermissionsAsync(factory, name));
    }

    [Fact]
    public async Task Viewer_cannot_create_roles_even_with_antiforgery()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(await factory.CreateUserAsync(Roles.Viewer), ServerManagerWebFactory.DefaultUserPassword, Ct);
        var token = await client.GetAntiforgeryTokenAsync("/Servers", Ct);

        using var response = await client.SendAsync(Post("/Roles/Create", token, Form("Hack", Permissions.UserManage)), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_grant_permissions_it_does_not_have()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(await factory.CreateUserAsync(Roles.Admin), ServerManagerWebFactory.DefaultUserPassword, Ct);
        var token = await client.GetAntiforgeryTokenAsync("/Roles/Create", Ct);

        using var response = await client.SendAsync(Post("/Roles/Create", token, Form("Esc " + Guid.NewGuid().ToString("N")[..6], Permissions.PluginManage)), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_role_is_immutable_and_builtin_roles_cannot_be_deleted()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var superAdminId = await RoleIdAsync(factory, Roles.SuperAdmin);
        var viewerId = await RoleIdAsync(factory, Roles.Viewer);
        var token = await client.GetAntiforgeryTokenAsync("/Roles", Ct);

        using var edit = await client.SendAsync(Post($"/Roles/Edit/{superAdminId}", token, Form(Roles.SuperAdmin, Permissions.ServerView)), Ct);
        using var delete = await client.SendAsync(Post($"/Roles/Delete/{viewerId}", token, []), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Deleting_role_with_users_requires_reassignment()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        var roleName = await SingleRoleOfAsync(factory, email);
        var roleId = await RoleIdAsync(factory, roleName);
        var viewerId = await RoleIdAsync(factory, Roles.Viewer);
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var token = await client.GetAntiforgeryTokenAsync("/Roles", Ct);

        using var blocked = await client.SendAsync(Post($"/Roles/Delete/{roleId}", token, []), Ct);
        using var deleted = await client.SendAsync(Post($"/Roles/Delete/{roleId}", token, [new("reassignToRoleId", viewerId.ToString())]), Ct);

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(Roles.Viewer, await SingleRoleOfAsync(factory, email));
    }

    [Fact]
    public async Task Removing_permission_from_role_takes_effect_for_signed_in_user()
    {
        var email = await immediate.CreateUserWithPermissionsAsync(Permissions.DashboardView, Permissions.ServerView);
        var roleName = await SingleRoleOfAsync(immediate, email);
        var roleId = await RoleIdAsync(immediate, roleName);

        using var user = immediate.CreateTestClient();
        await user.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);
        using (var before = await user.GetAsync("/Servers", Ct))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var admin = immediate.CreateTestClient();
        await admin.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var token = await admin.GetAntiforgeryTokenAsync($"/Roles/Edit/{roleId}", Ct);
        using (var edited = await admin.SendAsync(Post($"/Roles/Edit/{roleId}", token, Form(roleName, Permissions.DashboardView)), Ct))
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        // Security stamp değişti: oturum düşer; yeniden girişte izin artık yoktur.
        using var after = await user.GetAsync("/Servers", Ct);
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("/Account/Login", after.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);

        using var again = immediate.CreateTestClient();
        await again.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);
        using var denied = await again.GetAsync("/Servers", Ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", denied.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seeder_does_not_re_add_permission_removed_from_builtin_role()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var developer = (await roles.FindByNameAsync(Roles.Developer))!;
            await roles.RemoveClaimAsync(developer, new System.Security.Claims.Claim(Permissions.ClaimType, Permissions.FileDownload));

            await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        }

        var permissions = await RolePermissionsAsync(factory, Roles.Developer);
        Assert.DoesNotContain(Permissions.FileDownload, permissions);
        Assert.Contains(Permissions.FileView, permissions);

        // Varsayılana döndür izni geri getirir.
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var token = await client.GetAntiforgeryTokenAsync("/Roles", Ct);
        using var reset = await client.SendAsync(Post($"/Roles/ResetToDefault/{await RoleIdAsync(factory, Roles.Developer)}", token, []), Ct);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Contains(Permissions.FileDownload, await RolePermissionsAsync(factory, Roles.Developer));
    }

    [Fact]
    public async Task Admin_role_has_roles_manage_by_default()
    {
        Assert.Contains(Permissions.RolesManage, await RolePermissionsAsync(factory, Roles.Admin));
        Assert.DoesNotContain(Permissions.RolesManage, await RolePermissionsAsync(factory, Roles.Operator));
    }

    [Fact]
    public async Task User_can_be_assigned_multiple_roles()
    {
        var email = await factory.CreateUserAsync(Roles.Viewer);
        Guid userId;
        using (var scope = factory.Services.CreateScope())
            userId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!.Id;

        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var token = await client.GetAntiforgeryTokenAsync($"/Users/Edit/{userId}", Ct);
        using var response = await client.SendAsync(Post($"/Users/Edit/{userId}", token, [
            new("Email", email), new("Roles", Roles.Viewer), new("Roles", Roles.Developer), new("IsActive", "true")
        ]), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope2 = factory.Services.CreateScope();
        var users = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var assigned = await users.GetRolesAsync((await users.FindByIdAsync(userId.ToString()))!);
        Assert.Equal([Roles.Developer, Roles.Viewer], assigned.Order(StringComparer.Ordinal));
    }

    private static List<KeyValuePair<string, string>> Form(string name, params string[] permissions)
    {
        var form = new List<KeyValuePair<string, string>> { new("Name", name), new("Description", "test") };
        form.AddRange(permissions.Select(p => new KeyValuePair<string, string>("Permissions", p)));
        return form;
    }

    private static HttpRequestMessage Post(string path, string? antiforgery, List<KeyValuePair<string, string>> form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (antiforgery is not null)
            request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, antiforgery);
        return request;
    }

    private static async Task<List<string>> RolePermissionsAsync(ServerManagerWebFactory app, string roleName)
    {
        using var scope = app.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = await roles.FindByNameAsync(roleName);
        Assert.NotNull(role);
        return (await roles.GetClaimsAsync(role)).Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value).Order(StringComparer.Ordinal).ToList();
    }

    private static async Task<Guid> RoleIdAsync(ServerManagerWebFactory app, string roleName)
    {
        using var scope = app.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>().FindByNameAsync(roleName))!.Id;
    }

    private static async Task<string> SingleRoleOfAsync(ServerManagerWebFactory app, string email)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.GetRolesAsync((await users.FindByEmailAsync(email))!)).Single();
    }
}
