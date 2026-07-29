using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step16_AddBons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientNom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientTelephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MontantTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontantRegle = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VendeurId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bons_Vendeurs_VendeurId",
                        column: x => x.VendeurId,
                        principalTable: "Vendeurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BonLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BonId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BonLignes_Bons_BonId",
                        column: x => x.BonId,
                        principalTable: "Bons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BonLignes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReglementBons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BonId = table.Column<int>(type: "int", nullable: false),
                    DateReglement = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    EncaisseParUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglementBons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglementBons_Bons_BonId",
                        column: x => x.BonId,
                        principalTable: "Bons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BonLignes_BonId",
                table: "BonLignes",
                column: "BonId");

            migrationBuilder.CreateIndex(
                name: "IX_BonLignes_ProductId",
                table: "BonLignes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Bons_Numero",
                table: "Bons",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bons_VendeurId",
                table: "Bons",
                column: "VendeurId");

            migrationBuilder.CreateIndex(
                name: "IX_ReglementBons_BonId",
                table: "ReglementBons",
                column: "BonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BonLignes");

            migrationBuilder.DropTable(
                name: "ReglementBons");

            migrationBuilder.DropTable(
                name: "Bons");
        }
    }
}
