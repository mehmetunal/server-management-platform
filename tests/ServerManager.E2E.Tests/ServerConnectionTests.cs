using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Interfaces.Services;
using ServerManager.E2E.Tests.Infrastructure;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.E2E.Tests;

[Collection(E2ECollection.Name)]
public sealed class ServerConnectionTests(E2EFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Add_server_pins_host_key_on_first_test_and_detects_mismatch()
    {
        await fixture.RequireAsync();
        var name = E2EFixture.Unique("e2e-conn");
        var serverId = await fixture.CreateServerAsync(name, E2EEnvironment.User!, useSudo: true, sudoPassword: E2EEnvironment.Password);
        try
        {
            var first = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().TestConnectionAsync(serverId, Ct));
            Assert.True(first.IsSuccess, first.Message);
            Assert.True(first.Data!.IsSuccess, first.Data.Message);
            Assert.True(first.Data.FingerprintTrustedNow);
            Assert.False(string.IsNullOrWhiteSpace(first.Data.HostKeyFingerprint));

            var second = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().TestConnectionAsync(serverId, Ct));
            Assert.True(second.Data!.IsSuccess, second.Data.Message);
            Assert.False(second.Data.FingerprintTrustedNow);
            Assert.Equal(first.Data.HostKeyFingerprint, second.Data.HostKeyFingerprint);

            var details = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().GetDetailsAsync(serverId, Ct));
            Assert.True(details.IsSuccess, details.Message);

            // Kayıtlı parmak izi değiştirilirse (MITM senaryosu) bağlantı reddedilmelidir.
            await fixture.AsAdminAsync(async sp =>
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                var server = await db.Servers.SingleAsync(s => s.Id == serverId, Ct);
                server.HostKeyFingerprint = "SHA256:" + Convert.ToBase64String(new byte[32]).TrimEnd('=');
                await db.SaveChangesAsync(Ct);
            });

            var mismatch = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().TestConnectionAsync(serverId, Ct));
            Assert.True(mismatch.IsSuccess, mismatch.Message);
            Assert.False(mismatch.Data!.IsSuccess);
            Assert.True(mismatch.Data.FingerprintMismatch, mismatch.Data.Message);
        }
        finally
        {
            await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().DeleteAsync(serverId, name, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Wrong_password_fails_without_pinning_host_key()
    {
        await fixture.RequireAsync();
        var name = E2EFixture.Unique("e2e-badpw");
        var serverId = await fixture.CreateServerAsync(name, E2EEnvironment.User!, useSudo: false, sudoPassword: null);
        try
        {
            await fixture.AsAdminAsync(async sp =>
            {
                var dto = (await sp.GetRequiredService<IServerService>().GetForEditAsync(serverId, Ct)).Data!;
                dto.Password = "kesinlikle-yanlis-parola";
                var updated = await sp.GetRequiredService<IServerService>().UpdateAsync(dto, Ct);
                Assert.True(updated.IsSuccess, E2EFixture.Describe(updated));
            });

            var result = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().TestConnectionAsync(serverId, Ct));
            Assert.True(result.IsSuccess, result.Message);
            Assert.False(result.Data!.IsSuccess);
            Assert.False(result.Data.FingerprintTrustedNow);
        }
        finally
        {
            await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().DeleteAsync(serverId, name, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Nopasswd_sudo_user_can_connect_and_run_docker_overview()
    {
        await fixture.RequireAsync();
        Assert.SkipWhen(E2EEnvironment.NoPasswordSudoUser is null, "SM_E2E_SSH_NOPASSWD_USER tanımlı değil.");

        var name = E2EFixture.Unique("e2e-nopw");
        var serverId = await fixture.CreateServerAsync(name, E2EEnvironment.NoPasswordSudoUser!, useSudo: true, sudoPassword: null);
        try
        {
            var test = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().TestConnectionAsync(serverId, Ct));
            Assert.True(test.Data!.IsSuccess, test.Data.Message);

            var overview = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IDockerService>().GetOverviewAsync(serverId, Ct));
            Assert.True(overview.IsSuccess, overview.Message);
            Assert.False(string.IsNullOrWhiteSpace(overview.Data!.EngineVersion));
        }
        finally
        {
            await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerService>().DeleteAsync(serverId, name, CancellationToken.None));
        }
    }
}
