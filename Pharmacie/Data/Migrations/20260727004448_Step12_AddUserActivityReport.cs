using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step12_AddUserActivityReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserActivityReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeletedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DeletedUserDisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedUserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DeletedUserRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedUserConnectionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DeletedByDisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivityReportJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalSales = table.Column<int>(type: "int", nullable: false),
                    TotalSalesAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalStockMovements = table.Column<int>(type: "int", nullable: false),
                    TotalGoodsReceipts = table.Column<int>(type: "int", nullable: false),
                    TotalPurchaseOrders = table.Column<int>(type: "int", nullable: false),
                    FirstActivityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActivityDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityReports_DeletedAt",
                table: "UserActivityReports",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityReports_DeletedUserDisplayName",
                table: "UserActivityReports",
                column: "DeletedUserDisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserActivityReports");
        }
    }
}
