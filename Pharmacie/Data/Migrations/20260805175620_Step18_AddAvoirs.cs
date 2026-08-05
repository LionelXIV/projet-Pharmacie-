using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step18_AddAvoirs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Avoirs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientNom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientTelephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NumeroIdentite = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MontantTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VendeurId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avoirs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Avoirs_Vendeurs_VendeurId",
                        column: x => x.VendeurId,
                        principalTable: "Vendeurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AvoirLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AvoirId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstLivre = table.Column<bool>(type: "bit", nullable: false),
                    DateLivraison = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvoirLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvoirLignes_Avoirs_AvoirId",
                        column: x => x.AvoirId,
                        principalTable: "Avoirs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AvoirLignes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvoirLignes_AvoirId",
                table: "AvoirLignes",
                column: "AvoirId");

            migrationBuilder.CreateIndex(
                name: "IX_AvoirLignes_ProductId",
                table: "AvoirLignes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Avoirs_Numero",
                table: "Avoirs",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Avoirs_VendeurId",
                table: "Avoirs",
                column: "VendeurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvoirLignes");

            migrationBuilder.DropTable(
                name: "Avoirs");
        }
    }
}
