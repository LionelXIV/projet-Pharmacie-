using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step15_AddDiscountToSaleLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SaleLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "SaleLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "SaleLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "SaleLines");
        }
    }
}
