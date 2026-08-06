using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecycler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Recycles",
                table: "ProductionLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecycledOutput",
                table: "Materials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Both flags default to false, and the seeder only fills an empty table — so
            // a database that already has its lines and materials would come out of this
            // migration with no line that recycles and no material to recycle into. The
            // screen would then refuse everything, with nothing on it to explain why.
            //
            // The recycler is found from the data, not its name: it is the line that does
            // none of the other three things. Only applied when exactly one line matches,
            // because a factory that has added lines this migration cannot recognise is
            // better served by a tick box in Master Data than by a guess.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF (SELECT COUNT(*) FROM "ProductionLines"
                        WHERE NOT "MakesRolls" AND NOT "FormsBags"
                          AND NOT "TakesRawMaterial" AND NOT "RecordsMachineSettings") = 1
                    THEN
                        UPDATE "ProductionLines" SET "Recycles" = TRUE
                        WHERE NOT "MakesRolls" AND NOT "FormsBags"
                          AND NOT "TakesRawMaterial" AND NOT "RecordsMachineSettings";
                    END IF;
                END $$;
                """);

            // The material is found by its code, which is the identity the system relies
            // on and the one thing a rename cannot touch.
            migrationBuilder.Sql(
                """
                UPDATE "Materials" SET "IsRecycledOutput" = TRUE
                WHERE "Code" = 'MAT0002'
                  AND NOT EXISTS (SELECT 1 FROM "Materials" WHERE "IsRecycledOutput");
                """);

            migrationBuilder.CreateTable(
                name: "RecyclerProductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftLineId = table.Column<int>(type: "integer", nullable: false),
                    ScrapWeight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    RecycledMaterialWeight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecyclerProductions", x => x.Id);
                    table.CheckConstraint("ck_recycler_weights", "\"ScrapWeight\" >= 0 AND \"RecycledMaterialWeight\" >= 0 AND (\"ScrapWeight\" > 0 OR \"RecycledMaterialWeight\" > 0)");
                    table.ForeignKey(
                        name: "FK_RecyclerProductions_ShiftLines_ShiftLineId",
                        column: x => x.ShiftLineId,
                        principalTable: "ShiftLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecyclerProductions_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_materials_recycled_output",
                table: "Materials",
                column: "IsRecycledOutput",
                unique: true,
                filter: "\"IsRecycledOutput\"");

            migrationBuilder.CreateIndex(
                name: "IX_RecyclerProductions_RecordedByUserId",
                table: "RecyclerProductions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "ux_recycler_shift_line",
                table: "RecyclerProductions",
                column: "ShiftLineId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecyclerProductions");

            migrationBuilder.DropIndex(
                name: "ux_materials_recycled_output",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Recycles",
                table: "ProductionLines");

            migrationBuilder.DropColumn(
                name: "IsRecycledOutput",
                table: "Materials");
        }
    }
}
