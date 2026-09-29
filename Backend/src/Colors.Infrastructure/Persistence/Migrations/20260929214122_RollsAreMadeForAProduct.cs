using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RollsAreMadeForAProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "Rolls",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ThicknessInSpec",
                table: "RollTestReports",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxThickness",
                table: "Products",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinThickness",
                table: "Products",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rolls_ProductId",
                table: "Rolls",
                column: "ProductId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_thickness_range",
                table: "Products",
                sql: "(\"MinThickness\" IS NULL OR \"MinThickness\" > 0) AND (\"MaxThickness\" IS NULL OR \"MaxThickness\" > 0) AND (\"MinThickness\" IS NULL OR \"MaxThickness\" IS NULL OR \"MinThickness\" <= \"MaxThickness\")");

            migrationBuilder.AddForeignKey(
                name: "FK_Rolls_Products_ProductId",
                table: "Rolls",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rolls_Products_ProductId",
                table: "Rolls");

            migrationBuilder.DropIndex(
                name: "IX_Rolls_ProductId",
                table: "Rolls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_thickness_range",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "ThicknessInSpec",
                table: "RollTestReports");

            migrationBuilder.DropColumn(
                name: "MaxThickness",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinThickness",
                table: "Products");
        }
    }
}
