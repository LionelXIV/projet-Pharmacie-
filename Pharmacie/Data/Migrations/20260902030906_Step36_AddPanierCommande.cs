using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step36_AddPanierCommande : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PanierCommandes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statut = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PanierCommandes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PanierCommandes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PanierCommandeLignes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PanierCommandeId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    QuantiteConseillee = table.Column<int>(type: "int", nullable: false),
                    QuantiteFinale = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Selectionne = table.Column<bool>(type: "bit", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PanierCommandeLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PanierCommandeLignes_PanierCommandes_PanierCommandeId",
                        column: x => x.PanierCommandeId,
                        principalTable: "PanierCommandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PanierCommandeLignes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PanierCommandeLignes_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PanierCommandeLignes_PanierCommandeId_ProductId",
                table: "PanierCommandeLignes",
                columns: new[] { "PanierCommandeId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PanierCommandeLignes_ProductId",
                table: "PanierCommandeLignes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PanierCommandeLignes_SupplierId",
                table: "PanierCommandeLignes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PanierCommandes_UserId_Statut",
                table: "PanierCommandes",
                columns: new[] { "UserId", "Statut" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PanierCommandeLignes");

            migrationBuilder.DropTable(
                name: "PanierCommandes");
        }
    }
}
