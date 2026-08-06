using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagingConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LargeBagsPerBag",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CountedAs",
                table: "Materials",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // "None", not "". An empty string is not one of the values the enum can
                // take, so every existing row would fail to read back — and the unique
                // index below, which permits one row per value other than None, would
                // see fourteen materials all claiming the same non-None value and
                // refuse to build at all.
                defaultValue: "None");

            // The three the system counts, matched on the material code. The code is the
            // identity the system relies on and the name is not — renaming "Large Bags"
            // is exactly the thing this column exists to survive.
            migrationBuilder.Sql(
                """
                UPDATE "Materials" SET "CountedAs" = 'LargeBag'     WHERE "Code" = 'MAT0012';
                UPDATE "Materials" SET "CountedAs" = 'SmallBag'     WHERE "Code" = 'MAT0013';
                UPDATE "Materials" SET "CountedAs" = 'WoodenPallet' WHERE "Code" = 'MAT0014';
                """);

            // A plate goes into a big bag holding two small ones; a meal box or clamshell
            // goes into the small bag directly. Read off the small-bag figure here, once,
            // on data that already exists — which is the guess the column was added to
            // stop the running system making, and it is safe only because this runs
            // against what the factory has today.
            migrationBuilder.Sql(
                """
                UPDATE "Products" SET "LargeBagsPerBag" = 1 WHERE "SmallBagsPerBag" > 1;
                """);

            migrationBuilder.CreateTable(
                name: "PackagingConsumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftLineId = table.Column<int>(type: "integer", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagingConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackagingConsumptions_ShiftLines_ShiftLineId",
                        column: x => x.ShiftLineId,
                        principalTable: "ShiftLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackagingConsumptions_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackagingConsumptionLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsumptionId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    WasCounted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagingConsumptionLines", x => x.Id);
                    table.CheckConstraint("ck_packaging_lines_positive", "\"Quantity\" >= 0 AND (\"Weight\" IS NULL OR \"Weight\" > 0)");
                    table.ForeignKey(
                        name: "FK_PackagingConsumptionLines_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackagingConsumptionLines_PackagingConsumptions_Consumption~",
                        column: x => x.ConsumptionId,
                        principalTable: "PackagingConsumptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_materials_counted_as",
                table: "Materials",
                column: "CountedAs",
                unique: true,
                filter: "\"CountedAs\" <> 'None'");

            migrationBuilder.CreateIndex(
                name: "IX_PackagingConsumptionLines_MaterialId",
                table: "PackagingConsumptionLines",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "ux_packaging_lines_material",
                table: "PackagingConsumptionLines",
                columns: new[] { "ConsumptionId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackagingConsumptions_RecordedByUserId",
                table: "PackagingConsumptions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "ux_packaging_shift_line",
                table: "PackagingConsumptions",
                column: "ShiftLineId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackagingConsumptionLines");

            migrationBuilder.DropTable(
                name: "PackagingConsumptions");

            migrationBuilder.DropIndex(
                name: "ux_materials_counted_as",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "LargeBagsPerBag",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CountedAs",
                table: "Materials");
        }
    }
}
