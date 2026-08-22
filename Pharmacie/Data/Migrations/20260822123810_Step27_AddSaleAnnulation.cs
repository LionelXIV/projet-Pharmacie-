using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step27_AddSaleAnnulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnuleeParNom",
                table: "Sales",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnuleeParUserId",
                table: "Sales",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAnnulation",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnnulee",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RaisonAnnulation",
                table: "Sales",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnuleeParNom",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "AnnuleeParUserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DateAnnulation",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IsAnnulee",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RaisonAnnulation",
                table: "Sales");
        }
    }
}
