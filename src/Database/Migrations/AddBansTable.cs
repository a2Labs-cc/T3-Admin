using FluentMigrator;

namespace Furien_Admin.Database.Migrations;

[Migration(2025123102)]
public class AddBansTable : Migration
{
    public override void Up()
    {
        if (Schema.Table("t3_bans").Exists())
        {
            return;
        }

        Create.Table("t3_bans")
            .WithColumn("id").AsInt64().PrimaryKey().Identity().NotNullable()
            .WithColumn("steamid").AsInt64().NotNullable()
            .WithColumn("admin_name").AsString(64).NotNullable()
            .WithColumn("admin_steamid").AsInt64().NotNullable()
            .WithColumn("reason").AsString(2048).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime)
            .WithColumn("expires_at").AsDateTime().Nullable()
            .WithColumn("status").AsString(16).NotNullable().WithDefaultValue("active")
            .WithColumn("unban_admin_name").AsString(64).Nullable()
            .WithColumn("unban_admin_steamid").AsInt64().Nullable()
            .WithColumn("unban_reason").AsString(2048).Nullable()
            .WithColumn("unban_date").AsDateTime().Nullable();

        Create.Index("idx_t3_bans_steamid_status").OnTable("t3_bans").OnColumn("steamid").Ascending().OnColumn("status").Ascending();
        Create.Index("idx_t3_bans_expires_status").OnTable("t3_bans").OnColumn("expires_at").Ascending().OnColumn("status").Ascending();
        Create.Index("idx_t3_bans_created_at").OnTable("t3_bans").OnColumn("created_at");
        Create.Index("idx_t3_bans_status").OnTable("t3_bans").OnColumn("status");
    }

    public override void Down()
    {
        Delete.Table("t3_bans");
    }
}
