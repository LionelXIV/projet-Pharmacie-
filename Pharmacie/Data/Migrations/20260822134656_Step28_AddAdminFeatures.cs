using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step28_AddAdminFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdminTest",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdminTest",
                table: "Sales");
        }
    }
}
