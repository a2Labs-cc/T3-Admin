using FluentMigrator;

namespace Furien_Admin.Database.Migrations;

[Migration(2025123103)]
public class AddGagsTable : Migration
{
    public override void Up()
    {
        if (Schema.Table("t3_gags").Exists())
        {
            return;
        }

        Create.Table("t3_gags")
            .WithColumn("id").AsInt64().PrimaryKey().Identity().NotNullable()
            .WithColumn("steamid").AsInt64().NotNullable()
            .WithColumn("admin_name").AsString(64).NotNullable()
            .WithColumn("admin_steamid").AsInt64().NotNullable()
            .WithColumn("reason").AsString(2048).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime)
            .WithColumn("expires_at").AsDateTime().Nullable()
            .WithColumn("status").AsString(16).NotNullable().WithDefaultValue("active")
            .WithColumn("ungag_admin_name").AsString(64).Nullable()
            .WithColumn("ungag_admin_steamid").AsInt64().Nullable()
            .WithColumn("ungag_reason").AsString(2048).Nullable()
            .WithColumn("ungag_date").AsDateTime().Nullable();

        Create.Index("idx_t3_gags_steamid_status").OnTable("t3_gags").OnColumn("steamid").Ascending().OnColumn("status").Ascending();
        Create.Index("idx_t3_gags_expires_status").OnTable("t3_gags").OnColumn("expires_at").Ascending().OnColumn("status").Ascending();
        Create.Index("idx_t3_gags_status").OnTable("t3_gags").OnColumn("status");
    }

    public override void Down()
    {
        Delete.Table("t3_gags");
    }
}
