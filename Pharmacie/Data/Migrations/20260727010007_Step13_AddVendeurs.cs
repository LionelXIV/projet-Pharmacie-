using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step13_AddVendeurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VendeurId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vendeurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CouleurTicket = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendeurs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_VendeurId",
                table: "Sales",
                column: "VendeurId");

            migrationBuilder.CreateIndex(
                name: "IX_Vendeurs_IsActif",
                table: "Vendeurs",
                column: "IsActif");

            migrationBuilder.CreateIndex(
                name: "IX_Vendeurs_Nom",
                table: "Vendeurs",
                column: "Nom");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Vendeurs_VendeurId",
                table: "Sales",
                column: "VendeurId",
                principalTable: "Vendeurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Vendeurs_VendeurId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "Vendeurs");

            migrationBuilder.DropIndex(
                name: "IX_Sales_VendeurId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VendeurId",
                table: "Sales");
        }
    }
}
