using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610050024, "Proje webhook ayarları ve deployment türü (geri dönüş / yeniden başlatma)")]
public class M024_ProjectEnvWebhookRollback : Migration
{
    public override void Up()
    {
        Alter.Table("DeploymentProjects")
            .AddColumn("AutoDeployOnPush").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("EncryptedWebhookSecret").AsString(int.MaxValue).Nullable()
            .AddColumn("WebhookLastDeliveryAt").AsDateTime2().Nullable()
            .AddColumn("WebhookLastDeliverySucceeded").AsBoolean().Nullable()
            .AddColumn("WebhookLastDeliveryMessage").AsString(500).Nullable();

        Alter.Table("Deployments")
            .AddColumn("Kind").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.DefaultConstraint().OnTable("Deployments").OnColumn("Kind");
        Delete.Column("Kind").FromTable("Deployments");

        Delete.DefaultConstraint().OnTable("DeploymentProjects").OnColumn("AutoDeployOnPush");
        Delete.Column("AutoDeployOnPush")
            .Column("EncryptedWebhookSecret")
            .Column("WebhookLastDeliveryAt")
            .Column("WebhookLastDeliverySucceeded")
            .Column("WebhookLastDeliveryMessage")
            .FromTable("DeploymentProjects");
    }
}
