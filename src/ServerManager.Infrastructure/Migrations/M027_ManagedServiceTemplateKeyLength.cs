using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

/// <summary>
/// Eklenti şablon anahtarları ad alanlıdır (<c>&lt;systemname&gt;.&lt;ad&gt;</c>, ör. <c>services.extra.meilisearch</c>);
/// 32 karakter yetmez. Sütun indekslenmediği için yalnızca uzunluk büyütülür, mevcut değerler korunur.
/// </summary>
[Migration(202610060027, "Servisler: eklenti şablon anahtarları için TemplateKey uzunluğu")]
public class M027_ManagedServiceTemplateKeyLength : Migration
{
    public override void Up()
    {
        Alter.Column("TemplateKey").OnTable("ManagedServices").AsString(96).NotNullable();
    }

    public override void Down()
    {
        Alter.Column("TemplateKey").OnTable("ManagedServices").AsString(32).NotNullable();
    }
}
