using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendaZap.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddWhatsAppAccounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "whats_app_accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                phone_number_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                access_token_encrypted = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                status = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_whats_app_accounts", x => x.id);
                table.ForeignKey(
                    name: "fk_whats_app_accounts_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_whats_app_accounts_phone_number_id",
            table: "whats_app_accounts",
            column: "phone_number_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_whats_app_accounts_tenant_id",
            table: "whats_app_accounts",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "whats_app_accounts");
    }
}
