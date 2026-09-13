using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step37_AddRemiseGlobaleSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FraisWave",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RemiseGlobaleMontant",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RemiseGlobaleType",
                table: "Sales",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "percent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FraisWave",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RemiseGlobaleMontant",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RemiseGlobaleType",
                table: "Sales");
        }
    }
}
