using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendaZap.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddProductCategoryIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Índice composto (tenant_id, status, category) para consultas filtradas por categoria
        migrationBuilder.CreateIndex(
            name: "ix_products_tenant_id_status_category",
            table: "products",
            columns: new[] { "tenant_id", "status", "category" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_products_tenant_id_status_category",
            table: "products");
    }
}
