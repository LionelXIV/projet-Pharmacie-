using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step17_AddNumeroIdentiteToBon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroIdentite",
                table: "Bons",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumeroIdentite",
                table: "Bons");
        }
    }
}
