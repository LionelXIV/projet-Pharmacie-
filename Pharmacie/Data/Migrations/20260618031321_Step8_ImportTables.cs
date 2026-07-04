using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacie.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step8_ImportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceImportLineId",
                table: "ProductBatches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_AspNetUsers_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ImportBatches_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "ImportLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchId = table.Column<int>(type: "int", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawCip = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RawRefha = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RawLibelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RawQtefact = table.Column<int>(type: "int", nullable: true),
                    RawPxFab = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RawPph = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ResolvedAction = table.Column<int>(type: "int", nullable: false, defaultValue: 4),
                    MatchedProductId = table.Column<int>(type: "int", nullable: true),
                    CreatedBatchId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportLines_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportLines_ProductBatches_CreatedBatchId",
                        column: x => x.CreatedBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportLines_Products_MatchedProductId",
                        column: x => x.MatchedProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportAnomalies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportLineId = table.Column<int>(type: "int", nullable: false),
                    AnomalyType = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedByUser = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Resolution = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAnomalies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportAnomalies_ImportLines_ImportLineId",
                        column: x => x.ImportLineId,
                        principalTable: "ImportLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_SourceImportLineId",
                table: "ProductBatches",
                column: "SourceImportLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportAnomalies_ImportLineId",
                table: "ImportAnomalies",
                column: "ImportLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ConfirmedByUserId",
                table: "ImportBatches",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_UploadedByUserId",
                table: "ImportBatches",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLines_CreatedBatchId",
                table: "ImportLines",
                column: "CreatedBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLines_ImportBatchId",
                table: "ImportLines",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLines_MatchedProductId",
                table: "ImportLines",
                column: "MatchedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLines_ResolvedAction",
                table: "ImportLines",
                column: "ResolvedAction");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_ImportLines_SourceImportLineId",
                table: "ProductBatches",
                column: "SourceImportLineId",
                principalTable: "ImportLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_ImportLines_SourceImportLineId",
                table: "ProductBatches");

            migrationBuilder.DropTable(
                name: "ImportAnomalies");

            migrationBuilder.DropTable(
                name: "ImportLines");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_SourceImportLineId",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "SourceImportLineId",
                table: "ProductBatches");
        }
    }
}
