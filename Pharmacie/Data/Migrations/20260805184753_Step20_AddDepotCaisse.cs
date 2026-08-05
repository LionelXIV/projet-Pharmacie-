using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step20_AddDepotCaisse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepotCaisses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionCaisseId = table.Column<int>(type: "int", nullable: false),
                    HeureDepot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MontantDepose = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoldeAvantDepot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoldeApresDepot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    EffectueParUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepotCaisses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepotCaisses_SessionCaisses_SessionCaisseId",
                        column: x => x.SessionCaisseId,
                        principalTable: "SessionCaisses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepotCaisses_HeureDepot",
                table: "DepotCaisses",
                column: "HeureDepot");

            migrationBuilder.CreateIndex(
                name: "IX_DepotCaisses_SessionCaisseId",
                table: "DepotCaisses",
                column: "SessionCaisseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepotCaisses");
        }
    }
}
