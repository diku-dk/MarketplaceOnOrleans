using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orleans.Migrations
{
    /// <inheritdoc />
    public partial class SellerMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "order_entries",
                schema: "public",
                columns: table => new
                {
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    seller_id = table.Column<int>(type: "integer", nullable: false),
                    package_id = table.Column<int>(type: "integer", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: true),
                    product_category = table.Column<string>(type: "text", nullable: true),
                    unit_price = table.Column<float>(type: "real", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    total_items = table.Column<float>(type: "real", nullable: false),
                    total_amount = table.Column<float>(type: "real", nullable: false),
                    total_incentive = table.Column<float>(type: "real", nullable: false),
                    total_invoice = table.Column<float>(type: "real", nullable: false),
                    freight_value = table.Column<float>(type: "real", nullable: false),
                    shipment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    order_status = table.Column<string>(type: "text", nullable: false),
                    delivery_status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_entries", x => new { x.customer_id, x.order_id, x.product_id });
                });

            migrationBuilder.CreateIndex(
                name: "order_entry_idx",
                schema: "public",
                table: "order_entries",
                columns: new[] { "customer_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "order_entry_open_idx",
                schema: "public",
                table: "order_entries",
                column: "order_status",
                filter: "order_status = 'INVOICED' OR order_status = 'PAYMENT_PROCESSED' OR order_status = 'READY_FOR_SHIPMENT' OR order_status = 'IN_TRANSIT'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_entries",
                schema: "public");
        }
    }
}
