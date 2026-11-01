using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductCodesAndBagCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BagNumberCounters",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    NextNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BagNumberCounters", x => x.Day);
                    table.CheckConstraint("ck_bag_number_positive", "\"NextNumber\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ProductColourCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ColorId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductColourCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductColourCodes_Colors_ColorId",
                        column: x => x.ColorId,
                        principalTable: "Colors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductColourCodes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductColourCodes_ColorId",
                table: "ProductColourCodes",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "ux_product_colour_codes_code",
                table: "ProductColourCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_colour_codes_product_colour",
                table: "ProductColourCodes",
                columns: new[] { "ProductId", "ColorId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BagNumberCounters");

            migrationBuilder.DropTable(
                name: "ProductColourCodes");
        }
    }
}
