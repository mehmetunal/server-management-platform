using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040018, "Sistem alarm kurallarını kaldırır ve kurulu eklentileri kapatır")]
public class M018_RemoveSeedDataAndDisablePlugins : Migration
{
    private const string Marker = "M018";

    public override void Up()
    {
        Execute.Sql("""
            UPDATE AlertRules
            SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = N'Sistem'
            WHERE IsDeleted = 0
              AND Id IN (
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000001',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000002',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000003',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000004',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000005',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000006',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000007',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000008',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000009',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000010',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000011'
              );
            """);

        Execute.Sql($"""
            UPDATE InstalledPlugins
            SET IsEnabled = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = N'{Marker}'
            WHERE IsEnabled = 1;
            """);
    }

    public override void Down()
    {
        Execute.Sql("""
            UPDATE AlertRules
            SET IsDeleted = 0, DeletedAt = NULL, DeletedBy = NULL
            WHERE DeletedBy = N'Sistem'
              AND Id IN (
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000001',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000002',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000003',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000004',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000005',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000006',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000007',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000008',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000009',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000010',
                '6f1e9a52-0c1d-4b9e-9d3a-1a0f00000011'
              );
            """);

        Execute.Sql($"""
            UPDATE InstalledPlugins
            SET IsEnabled = 1, UpdatedBy = NULL
            WHERE UpdatedBy = N'{Marker}';
            """);
    }
}
