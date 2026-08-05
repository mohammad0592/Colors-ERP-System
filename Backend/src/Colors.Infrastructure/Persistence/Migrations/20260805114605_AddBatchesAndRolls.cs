using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Line 1 — the mixer and the extruder (specification section 8): batches, rolls
    /// and the roll test report.
    ///
    /// `RecipeFamilies` gains the `Code` that forms the family's part of a roll code.
    /// The seeder only fills an empty table, so a database that already has its four
    /// families would be left with blanks and no roll could be given a code at all.
    /// </summary>
    public partial class AddBatchesAndRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "batch_number_seq");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "RecipeFamilies",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            // Derived from the absorbency flag, never from the family's name — the same
            // rule the rest of the system follows. Normal and Normal Black both become
            // "N"; the colour letter in the roll code is what separates them.
            migrationBuilder.Sql(
                """
                UPDATE "RecipeFamilies"
                SET "Code" = CASE WHEN "IsAbsorbent" THEN 'Abs' ELSE 'N' END
                WHERE "Code" = '';
                """);

            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchNumber = table.Column<int>(type: "integer", nullable: false),
                    ShiftLineId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Batches_ShiftLines_ShiftLineId",
                        column: x => x.ShiftLineId,
                        principalTable: "ShiftLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Batches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DailySerial = table.Column<int>(type: "integer", nullable: false),
                    RollCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    RecipeVersionId = table.Column<int>(type: "integer", nullable: false),
                    ColorId = table.Column<int>(type: "integer", nullable: false),
                    ProducedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ProducedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rolls_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rolls_Colors_ColorId",
                        column: x => x.ColorId,
                        principalTable: "Colors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rolls_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rolls_Users_ProducedByUserId",
                        column: x => x.ProducedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RollTestReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RollId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    Length = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    PlateWeight = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    ThicknessRs = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    ThicknessRm = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    ThicknessLm = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    ThicknessLs = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    TestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    TestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RollTestReports", x => x.Id);
                    table.CheckConstraint("ck_roll_tests_positive", "\"Weight\" > 0 AND \"Length\" > 0 AND \"PlateWeight\" > 0");
                    table.ForeignKey(
                        name: "FK_RollTestReports_Rolls_RollId",
                        column: x => x.RollId,
                        principalTable: "Rolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RollTestReports_Users_TestedByUserId",
                        column: x => x.TestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Batches_CreatedByUserId",
                table: "Batches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_batches_shift_line",
                table: "Batches",
                column: "ShiftLineId");

            migrationBuilder.CreateIndex(
                name: "ux_batches_number",
                table: "Batches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RollTestReports_TestedByUserId",
                table: "RollTestReports",
                column: "TestedByUserId");

            migrationBuilder.CreateIndex(
                name: "ux_roll_tests_roll",
                table: "RollTestReports",
                column: "RollId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rolls_BatchId",
                table: "Rolls",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Rolls_ColorId",
                table: "Rolls",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_Rolls_ProducedByUserId",
                table: "Rolls",
                column: "ProducedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rolls_RecipeVersionId",
                table: "Rolls",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_rolls_status",
                table: "Rolls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_rolls_code",
                table: "Rolls",
                column: "RollCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rolls_date_serial",
                table: "Rolls",
                columns: new[] { "ProductionDate", "DailySerial" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RollTestReports");

            migrationBuilder.DropTable(
                name: "Rolls");

            migrationBuilder.DropTable(
                name: "Batches");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "RecipeFamilies");

            migrationBuilder.DropSequence(
                name: "batch_number_seq");
        }
    }
}
