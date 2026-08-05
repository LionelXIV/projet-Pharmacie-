using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step19_AddSessionCaisse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionCaisses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroCaisse = table.Column<int>(type: "int", nullable: false),
                    DateSession = table.Column<DateTime>(type: "date", nullable: false),
                    HeureOuverture = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeureFermeture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FondDepart = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CaissierUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BilletageTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BilletageJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCaisses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VenteCaisses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionCaisseId = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenteCaisses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenteCaisses_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VenteCaisses_SessionCaisses_SessionCaisseId",
                        column: x => x.SessionCaisseId,
                        principalTable: "SessionCaisses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionCaisses_NumeroCaisse_DateSession_Statut",
                table: "SessionCaisses",
                columns: new[] { "NumeroCaisse", "DateSession", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_VenteCaisses_SaleId",
                table: "VenteCaisses",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_VenteCaisses_SessionCaisseId_SaleId",
                table: "VenteCaisses",
                columns: new[] { "SessionCaisseId", "SaleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VenteCaisses");

            migrationBuilder.DropTable(
                name: "SessionCaisses");
        }
    }
}
