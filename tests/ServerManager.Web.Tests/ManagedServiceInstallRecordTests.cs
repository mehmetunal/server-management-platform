using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>
/// Regresyon: servis kaydı ve ilk "kurulum" işlemi aynı SaveChanges'te eklenir. EF modelinde
/// ManagedServiceOperation → ManagedService ilişkisi yoksa işlem satırı önce yazılır ve gerçek SQL Server'da
/// FK_ManagedServiceOperations_ManagedServices hatası alınır (E2E testleri yakaladı). SSH'a gidilmez.
/// </summary>
[Collection(WebCollection.Name)]
public sealed class ManagedServiceInstallRecordTests(ServerManagerWebFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Begin_install_saves_service_and_operation_together()
    {
        using var _ = factory.CreateTestClient(); // SM_TEST_SQL yoksa atlar.

        var serverId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Servers.Add(new Server { Id = serverId, Name = "ms-" + serverId.ToString("N")[..8], Hostname = "ms", IpAddress = "203.0.113.40", Username = "deploy" });
            await db.SaveChangesAsync(Ct);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider.GetRequiredService<IManagedServiceService>();
            var result = await services.BeginInstallAsync(new CreateManagedServiceDto
            {
                ServerId = serverId,
                TemplateKey = ServiceTemplates.Redis,
                Name = "redis-kayit",
                Ports = [new ServicePortFormItem { ContainerPort = 6379, Publish = false }]
            }, new ServiceActor(null, "test", null), Ct);

            Assert.True(result.IsSuccess, result.Message);
            var operation = await services.GetOperationAsync(result.Data!.OperationId, includeLog: false, Ct);
            Assert.True(operation.IsSuccess, operation.Message);
            Assert.Equal(result.Data.ServiceId, operation.Data!.ServiceId);
        }
    }
}
