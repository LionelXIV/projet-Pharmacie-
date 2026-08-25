using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step32_AddBilletageModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BilletageModifieAt",
                table: "SessionCaisses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BilletageModifiePar",
                table: "SessionCaisses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BilletageRaisonModification",
                table: "SessionCaisses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BilletageModifieAt",
                table: "SessionCaisses");

            migrationBuilder.DropColumn(
                name: "BilletageModifiePar",
                table: "SessionCaisses");

            migrationBuilder.DropColumn(
                name: "BilletageRaisonModification",
                table: "SessionCaisses");
        }
    }
}
