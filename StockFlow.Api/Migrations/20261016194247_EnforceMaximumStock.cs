using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceMaximumStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Quantity_NonNegative",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_QuantityInStock_Range",
                table: "Products",
                sql: "\"QuantityInStock\" >= 0 AND \"QuantityInStock\" <= 100000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_QuantityInStock_Range",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Quantity_NonNegative",
                table: "Products",
                sql: "\"QuantityInStock\" >= 0");
        }
    }
}
