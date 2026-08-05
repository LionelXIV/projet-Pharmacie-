using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step22_AddRolesAndPrixModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrixModifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AncienPrix = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NouveauPrix = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ModifiedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Raison = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrixModifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrixModifications_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrixModifications_ModifiedAt",
                table: "PrixModifications",
                column: "ModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrixModifications_ProductId",
                table: "PrixModifications",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrixModifications");
        }
    }
}
