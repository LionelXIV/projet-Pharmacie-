using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step33_AddVenteModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsModifiee",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VenteOriginaleId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleId",
                table: "PrixModifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrixModifications_SaleId",
                table: "PrixModifications",
                column: "SaleId");

            migrationBuilder.AddForeignKey(
                name: "FK_PrixModifications_Sales_SaleId",
                table: "PrixModifications",
                column: "SaleId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrixModifications_Sales_SaleId",
                table: "PrixModifications");

            migrationBuilder.DropIndex(
                name: "IX_PrixModifications_SaleId",
                table: "PrixModifications");

            migrationBuilder.DropColumn(
                name: "IsModifiee",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VenteOriginaleId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SaleId",
                table: "PrixModifications");
        }
    }
}
