using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step35_AddProductStockFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClasseABC",
                table: "Products",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "C");

            migrationBuilder.AddColumn<int>(
                name: "StockMaximum",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClasseABC",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StockMaximum",
                table: "Products");
        }
    }
}
