using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendaZap.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── tenants ───────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "tenants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                whats_app_phone_number_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                whats_app_access_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                whats_app_business_account_id = table.Column<string>(type: "text", nullable: true),
                open_ai_assistant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                plan = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                logo_url = table.Column<string>(type: "text", nullable: true),
                welcome_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                away_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                is_human_takeover_enabled = table.Column<bool>(type: "boolean", nullable: false),
                max_concurrent_conversations = table.Column<int>(type: "integer", nullable: false),
                trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                subscription_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenants", x => x.id);
            });

        // ── users ─────────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                role = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                refresh_token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                refresh_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
                table.ForeignKey(
                    name: "fk_users_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── products ──────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                // Money owned entity columns (explicit HasColumnName)
                Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                external_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                stock_quantity = table.Column<int>(type: "integer", nullable: false),
                track_stock = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_products", x => x.id);
                table.ForeignKey(
                    name: "fk_products_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── contacts ──────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "contacts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                zip_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                notes = table.Column<string>(type: "text", nullable: true),
                is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                last_interaction_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                total_orders = table.Column<int>(type: "integer", nullable: false),
                total_spent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                tags_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: "[]"),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_contacts", x => x.id);
                table.ForeignKey(
                    name: "fk_contacts_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── campaigns ─────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "campaigns",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                message_template = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                media_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                trigger = table.Column<int>(type: "integer", nullable: false),
                trigger_delay_minutes = table.Column<int>(type: "integer", nullable: false),
                scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                active_days = table.Column<int[]>(type: "integer[]", nullable: true),
                active_from = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                active_to = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                total_sent = table.Column<int>(type: "integer", nullable: false),
                total_delivered = table.Column<int>(type: "integer", nullable: false),
                total_read = table.Column<int>(type: "integer", nullable: false),
                total_replied = table.Column<int>(type: "integer", nullable: false),
                filters_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: "{}"),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_campaigns", x => x.id);
                table.ForeignKey(
                    name: "fk_campaigns_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── auto_reply_templates ──────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "auto_reply_templates",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                triggers = table.Column<string>(type: "text", nullable: false),
                response = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_auto_reply_templates", x => x.id);
            });

        // ── conversations ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "conversations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<int>(type: "integer", nullable: false),
                mode = table.Column<int>(type: "integer", nullable: false),
                stage = table.Column<int>(type: "integer", nullable: false),
                last_message_preview = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                unread_count = table.Column<int>(type: "integer", nullable: false),
                whats_app_conversation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ai_thread_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                cart_json = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false, defaultValue: "{}"),
                active_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_conversations", x => x.id);
                table.ForeignKey(
                    name: "fk_conversations_contacts_contact_id",
                    column: x => x.contact_id,
                    principalTable: "contacts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_conversations_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_conversations_users_assigned_to_user_id",
                    column: x => x.assigned_to_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        // ── messages ──────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                content = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                direction = table.Column<int>(type: "integer", nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                source = table.Column<int>(type: "integer", nullable: false),
                whats_app_message_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                media_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                media_mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                template_id = table.Column<string>(type: "text", nullable: true),
                error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_messages", x => x.id);
                table.ForeignKey(
                    name: "fk_messages_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalTable: "conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── orders ────────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                payment_method = table.Column<int>(type: "integer", nullable: false),
                payment_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                payment_instructions = table.Column<string>(type: "text", nullable: true),
                pix_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                // Money owned entity columns (explicit HasColumnName)
                Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                SubtotalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                ShippingCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                ShippingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TotalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                delivery_address = table.Column<string>(type: "text", nullable: true),
                delivery_city = table.Column<string>(type: "text", nullable: true),
                delivery_state = table.Column<string>(type: "text", nullable: true),
                delivery_zip_code = table.Column<string>(type: "text", nullable: true),
                notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                shipped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancellation_reason = table.Column<string>(type: "text", nullable: true),
                tracking_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_orders", x => x.id);
                table.ForeignKey(
                    name: "fk_orders_contacts_contact_id",
                    column: x => x.contact_id,
                    principalTable: "contacts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_orders_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalTable: "conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_orders_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── order_items ───────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "order_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                // Money owned entity columns (explicit HasColumnName)
                UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                UnitPriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TotalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_items", x => x.id);
                table.ForeignKey(
                    name: "fk_order_items_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_order_items_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── indexes ───────────────────────────────────────────────────────────

        // tenants
        migrationBuilder.CreateIndex(
            name: "ix_tenants_slug",
            table: "tenants",
            column: "slug",
            unique: true);

        // users
        migrationBuilder.CreateIndex(
            name: "ix_users_email_tenant_id",
            table: "users",
            columns: new[] { "email", "tenant_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_tenant_id",
            table: "users",
            column: "tenant_id");

        // products
        migrationBuilder.CreateIndex(
            name: "ix_products_tenant_id",
            table: "products",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_products_tenant_id_status",
            table: "products",
            columns: new[] { "tenant_id", "status" });

        // contacts
        migrationBuilder.CreateIndex(
            name: "ix_contacts_phone_number_tenant_id",
            table: "contacts",
            columns: new[] { "phone_number", "tenant_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_contacts_tenant_id",
            table: "contacts",
            column: "tenant_id");

        // campaigns
        migrationBuilder.CreateIndex(
            name: "ix_campaigns_tenant_id_status",
            table: "campaigns",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_campaigns_tenant_id",
            table: "campaigns",
            column: "tenant_id");

        // auto_reply_templates
        migrationBuilder.CreateIndex(
            name: "ix_auto_reply_templates_tenant_id_is_active",
            table: "auto_reply_templates",
            columns: new[] { "tenant_id", "is_active" });

        // conversations
        migrationBuilder.CreateIndex(
            name: "ix_conversations_tenant_id_status",
            table: "conversations",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_conversations_tenant_id_contact_id_status",
            table: "conversations",
            columns: new[] { "tenant_id", "contact_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_conversations_contact_id",
            table: "conversations",
            column: "contact_id");

        migrationBuilder.CreateIndex(
            name: "ix_conversations_assigned_to_user_id",
            table: "conversations",
            column: "assigned_to_user_id");

        // messages
        migrationBuilder.CreateIndex(
            name: "ix_messages_whats_app_message_id",
            table: "messages",
            column: "whats_app_message_id");

        migrationBuilder.CreateIndex(
            name: "ix_messages_conversation_id_created_at",
            table: "messages",
            columns: new[] { "conversation_id", "created_at" });

        // orders
        migrationBuilder.CreateIndex(
            name: "ix_orders_tenant_id_order_number",
            table: "orders",
            columns: new[] { "tenant_id", "order_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_orders_tenant_id_status",
            table: "orders",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_orders_contact_id",
            table: "orders",
            column: "contact_id");

        migrationBuilder.CreateIndex(
            name: "ix_orders_conversation_id",
            table: "orders",
            column: "conversation_id");

        // order_items
        migrationBuilder.CreateIndex(
            name: "ix_order_items_order_id",
            table: "order_items",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_product_id",
            table: "order_items",
            column: "product_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "order_items");
        migrationBuilder.DropTable(name: "messages");
        migrationBuilder.DropTable(name: "orders");
        migrationBuilder.DropTable(name: "conversations");
        migrationBuilder.DropTable(name: "auto_reply_templates");
        migrationBuilder.DropTable(name: "campaigns");
        migrationBuilder.DropTable(name: "products");
        migrationBuilder.DropTable(name: "contacts");
        migrationBuilder.DropTable(name: "users");
        migrationBuilder.DropTable(name: "tenants");
    }
}
