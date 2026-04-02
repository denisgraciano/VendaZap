using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendaZap.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddConversationStatusIndexAndWaitingHuman : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Index for AI context queries: filter by tenant + status + time range
        migrationBuilder.CreateIndex(
            name: "ix_conversations_tenant_id_status_created_at",
            table: "conversations",
            columns: new[] { "tenant_id", "status", "created_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_conversations_tenant_id_status_created_at",
            table: "conversations");
    }
}
