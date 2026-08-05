using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddThermoforming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FormsBags",
                table: "ProductionLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MakesRolls",
                table: "ProductionLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TakesRawMaterial",
                table: "ProductionLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The three columns arrive false, which on an existing database would leave
            // every line unable to do anything: no batch could be started, no ticket
            // written, no roll formed. So they are filled in before anything else runs.
            //
            // The thermo is identified from the data, not from its name — it is the line
            // that records machine settings, which has been true since those settings
            // existed.
            migrationBuilder.Sql(
                """
                UPDATE "ProductionLines" SET "FormsBags" = TRUE
                WHERE "RecordsMachineSettings";
                """);

            // The extruder has no such column to lean on, so this one time its name is
            // used. Acceptable here because a migration is a fix applied once to data
            // that already exists, and the running system never looks at a name again.
            // Anything else the factory has renamed is a tick box in Master Data.
            migrationBuilder.Sql(
                """
                UPDATE "ProductionLines" SET "MakesRolls" = TRUE, "TakesRawMaterial" = TRUE
                WHERE "Name" = 'Extruder';
                """);

            migrationBuilder.CreateTable(
                name: "ThermoProductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RollId = table.Column<int>(type: "integer", nullable: false),
                    ShiftLineId = table.Column<int>(type: "integer", nullable: false),
                    OperatorUserId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThermoProductions", x => x.Id);
                    table.CheckConstraint("ck_thermo_finished_after_started", "\"FinishedAt\" IS NULL OR \"FinishedAt\" >= \"StartedAt\"");
                    table.ForeignKey(
                        name: "FK_ThermoProductions_Rolls_RollId",
                        column: x => x.RollId,
                        principalTable: "Rolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ThermoProductions_ShiftLines_ShiftLineId",
                        column: x => x.ShiftLineId,
                        principalTable: "ShiftLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ThermoProductions_Users_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProducedBags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThermoProductionId = table.Column<int>(type: "integer", nullable: false),
                    ColorId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    PieceCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProducedBags", x => x.Id);
                    table.CheckConstraint("ck_produced_bags_positive", "\"Weight\" > 0 AND \"PieceCount\" > 0");
                    table.ForeignKey(
                        name: "FK_ProducedBags_Colors_ColorId",
                        column: x => x.ColorId,
                        principalTable: "Colors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProducedBags_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProducedBags_ThermoProductions_ThermoProductionId",
                        column: x => x.ThermoProductionId,
                        principalTable: "ThermoProductions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThermoTestReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThermoProductionId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    BagCount = table.Column<int>(type: "integer", nullable: false),
                    PieceCount = table.Column<int>(type: "integer", nullable: false),
                    PieceWeight = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    BagWeight = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    AbsorbentPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    TestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThermoTestReports", x => x.Id);
                    table.CheckConstraint("ck_thermo_tests_positive", "\"BagCount\" > 0 AND \"PieceCount\" > 0 AND \"PieceWeight\" > 0 AND \"BagWeight\" > 0 AND \"AbsorbentPercentage\" >= 0 AND \"AbsorbentPercentage\" <= 100");
                    table.ForeignKey(
                        name: "FK_ThermoTestReports_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ThermoTestReports_ThermoProductions_ThermoProductionId",
                        column: x => x.ThermoProductionId,
                        principalTable: "ThermoProductions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ThermoTestReports_Users_TestedByUserId",
                        column: x => x.TestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProducedBags_ColorId",
                table: "ProducedBags",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProducedBags_ProductId",
                table: "ProducedBags",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_produced_bags_production",
                table: "ProducedBags",
                column: "ThermoProductionId");

            migrationBuilder.CreateIndex(
                name: "ix_produced_bags_status",
                table: "ProducedBags",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ThermoProductions_OperatorUserId",
                table: "ThermoProductions",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "ix_thermo_productions_shift_line",
                table: "ThermoProductions",
                column: "ShiftLineId");

            migrationBuilder.CreateIndex(
                name: "ux_thermo_productions_roll",
                table: "ThermoProductions",
                column: "RollId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThermoTestReports_ProductId",
                table: "ThermoTestReports",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ThermoTestReports_TestedByUserId",
                table: "ThermoTestReports",
                column: "TestedByUserId");

            migrationBuilder.CreateIndex(
                name: "ux_thermo_tests_production",
                table: "ThermoTestReports",
                column: "ThermoProductionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProducedBags");

            migrationBuilder.DropTable(
                name: "ThermoTestReports");

            migrationBuilder.DropTable(
                name: "ThermoProductions");

            migrationBuilder.DropColumn(
                name: "FormsBags",
                table: "ProductionLines");

            migrationBuilder.DropColumn(
                name: "MakesRolls",
                table: "ProductionLines");

            migrationBuilder.DropColumn(
                name: "TakesRawMaterial",
                table: "ProductionLines");
        }
    }
}
