using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A shift becomes one record per date and shift for the whole factory, with the
    /// lines that ran hanging underneath (specification section 2).
    ///
    /// The scaffolded version dropped the line, times and meter columns straight away,
    /// which would have thrown away every shift already recorded. The order here moves
    /// the data first and drops the columns last, so nothing is lost:
    ///
    ///   1. create ShiftLines
    ///   2. give every existing report a line, carrying its times, meter and settings
    ///   3. repoint the workers at that line
    ///   4. merge reports that shared a date and shift — they are now one shift
    ///   5. only then drop the old columns and add the new unique index
    /// </summary>
    public partial class ShiftPerDayWithLines : Migration
    {
        /// <summary>
        /// One row per (date, shift), the earliest opened winning. Written once and
        /// reused, because every step of the merge has to agree on which row stays.
        /// </summary>
        private const string Keeper =
            """
            WITH keeper AS (
                SELECT DISTINCT ON ("ProductionDate", "ShiftId")
                       "Id", "ProductionDate", "ShiftId"
                FROM "ShiftReports"
                ORDER BY "ProductionDate", "ShiftId", "OpenedAt", "Id"
            )
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftReports_ProductionLines_ProductionLineId",
                table: "ShiftReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftWorkers_ShiftReports_ShiftReportId",
                table: "ShiftWorkers");

            migrationBuilder.DropIndex(
                name: "ix_shift_reports_line_date",
                table: "ShiftReports");

            migrationBuilder.DropIndex(
                name: "ux_shift_reports_line_shift_date",
                table: "ShiftReports");

            // On for every line, which keeps exactly today's behaviour. When the
            // factory says how many meters it has, the lines without one are unticked
            // in Master Data — no code, no migration.
            migrationBuilder.AddColumn<bool>(
                name: "RecordsElectricity",
                table: "ProductionLines",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ShiftLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftReportId = table.Column<int>(type: "integer", nullable: false),
                    ProductionLineId = table.Column<int>(type: "integer", nullable: false),
                    ProductionStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ProductionEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DowntimeHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    ElectricityStartMeter = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    ElectricityEndMeter = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    MachineSpeed = table.Column<int>(type: "integer", nullable: true),
                    FeedDistanceMm = table.Column<int>(type: "integer", nullable: true),
                    CycleTimeSeconds = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftLines_ProductionLines_ProductionLineId",
                        column: x => x.ProductionLineId,
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftLines_ShiftReports_ShiftReportId",
                        column: x => x.ShiftReportId,
                        principalTable: "ShiftReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Every report becomes one line, keeping everything it recorded.
            migrationBuilder.Sql(
                """
                INSERT INTO "ShiftLines" (
                    "ShiftReportId", "ProductionLineId",
                    "ProductionStartTime", "ProductionEndTime", "DowntimeHours",
                    "ElectricityStartMeter", "ElectricityEndMeter",
                    "MachineSpeed", "FeedDistanceMm", "CycleTimeSeconds")
                SELECT
                    "Id", "ProductionLineId",
                    "ProductionStartTime", "ProductionEndTime", "DowntimeHours",
                    "ElectricityStartMeter", "ElectricityEndMeter",
                    "MachineSpeed", "FeedDistanceMm", "CycleTimeSeconds"
                FROM "ShiftReports";
                """);

            migrationBuilder.RenameColumn(
                name: "ShiftReportId",
                table: "ShiftWorkers",
                newName: "ShiftLineId");

            migrationBuilder.RenameIndex(
                name: "ux_shift_workers_report_user",
                table: "ShiftWorkers",
                newName: "ux_shift_workers_line_user");

            // 3. The column now holds a report id. Swap it for the line's id — one
            //    line per report at this point, so the match is exact.
            migrationBuilder.Sql(
                """
                UPDATE "ShiftWorkers" w
                SET "ShiftLineId" = l."Id"
                FROM "ShiftLines" l
                WHERE l."ShiftReportId" = w."ShiftLineId";
                """);

            // 4a. Reports that shared a date and shift are one shift now: move their
            //     lines onto the keeper.
            migrationBuilder.Sql(
                $"""
                {Keeper}
                UPDATE "ShiftLines" sl
                SET "ShiftReportId" = k."Id"
                FROM "ShiftReports" r
                JOIN keeper k
                  ON k."ProductionDate" = r."ProductionDate" AND k."ShiftId" = r."ShiftId"
                WHERE sl."ShiftReportId" = r."Id" AND r."Id" <> k."Id";
                """);

            // 4b. A shift is not finished while any of its lines was still running, so
            //     the merged shift is Open if any of the reports it came from was.
            migrationBuilder.Sql(
                $"""
                {Keeper}, grouped AS (
                    SELECT k."Id" AS keeper_id, bool_or(r."Status" = 'Open') AS any_open
                    FROM keeper k
                    JOIN "ShiftReports" r
                      ON r."ProductionDate" = k."ProductionDate" AND r."ShiftId" = k."ShiftId"
                    GROUP BY k."Id"
                )
                UPDATE "ShiftReports" sr
                SET "Status" = 'Open', "ClosedAt" = NULL, "ClosedByUserId" = NULL
                FROM grouped
                WHERE sr."Id" = grouped.keeper_id
                  AND grouped.any_open
                  AND sr."Status" <> 'Open';
                """);

            // 4c. The rows that were merged away have no lines left on them.
            migrationBuilder.Sql(
                $"""
                {Keeper}
                DELETE FROM "ShiftReports" r
                WHERE NOT EXISTS (SELECT 1 FROM keeper k WHERE k."Id" = r."Id");
                """);

            // 5. Now the old columns have given up everything they held.
            foreach (var column in new[]
                     {
                         "ProductionLineId",
                         "ProductionStartTime",
                         "ProductionEndTime",
                         "DowntimeHours",
                         "ElectricityStartMeter",
                         "ElectricityEndMeter",
                         "MachineSpeed",
                         "FeedDistanceMm",
                         "CycleTimeSeconds",
                     })
            {
                migrationBuilder.DropColumn(name: column, table: "ShiftReports");
            }

            migrationBuilder.CreateIndex(
                name: "ux_shift_reports_date_shift",
                table: "ShiftReports",
                columns: new[] { "ProductionDate", "ShiftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shift_lines_line",
                table: "ShiftLines",
                column: "ProductionLineId");

            migrationBuilder.CreateIndex(
                name: "ux_shift_lines_report_line",
                table: "ShiftLines",
                columns: new[] { "ShiftReportId", "ProductionLineId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftWorkers_ShiftLines_ShiftLineId",
                table: "ShiftWorkers",
                column: "ShiftLineId",
                principalTable: "ShiftLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <summary>
        /// Going back splits each shift into one report per line again. A shift with
        /// two lines becomes two reports; only the first keeps the original id, so the
        /// others are new rows. The figures survive, the ids do not.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftWorkers_ShiftLines_ShiftLineId",
                table: "ShiftWorkers");

            migrationBuilder.DropIndex(
                name: "ux_shift_reports_date_shift",
                table: "ShiftReports");

            migrationBuilder.AddColumn<decimal>(
                name: "CycleTimeSeconds",
                table: "ShiftReports",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DowntimeHours",
                table: "ShiftReports",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityEndMeter",
                table: "ShiftReports",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityStartMeter",
                table: "ShiftReports",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeedDistanceMm",
                table: "ShiftReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MachineSpeed",
                table: "ShiftReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ProductionEndTime",
                table: "ShiftReports",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionLineId",
                table: "ShiftReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ProductionStartTime",
                table: "ShiftReports",
                type: "time without time zone",
                nullable: true);

            // Second and later lines of a shift become reports of their own.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT l."Id" AS line_id, l."ShiftReportId",
                           row_number() OVER (PARTITION BY l."ShiftReportId" ORDER BY l."Id") AS n
                    FROM "ShiftLines" l
                ),
                created AS (
                    INSERT INTO "ShiftReports" (
                        "ProductionDate", "ShiftId", "Status", "SupervisorUserId", "Notes",
                        "OpenedByUserId", "OpenedAt", "ClosedByUserId", "ClosedAt",
                        "ProductionLineId", "ProductionStartTime", "ProductionEndTime",
                        "DowntimeHours", "ElectricityStartMeter", "ElectricityEndMeter",
                        "MachineSpeed", "FeedDistanceMm", "CycleTimeSeconds")
                    SELECT r."ProductionDate", r."ShiftId", r."Status", r."SupervisorUserId", r."Notes",
                           r."OpenedByUserId", r."OpenedAt", r."ClosedByUserId", r."ClosedAt",
                           l."ProductionLineId", l."ProductionStartTime", l."ProductionEndTime",
                           l."DowntimeHours", l."ElectricityStartMeter", l."ElectricityEndMeter",
                           l."MachineSpeed", l."FeedDistanceMm", l."CycleTimeSeconds"
                    FROM ranked
                    JOIN "ShiftLines" l ON l."Id" = ranked.line_id
                    JOIN "ShiftReports" r ON r."Id" = l."ShiftReportId"
                    WHERE ranked.n > 1
                    RETURNING "Id"
                )
                SELECT count(*) FROM created;
                """);

            // The first line folds back into the report it already belonged to.
            migrationBuilder.Sql(
                """
                UPDATE "ShiftReports" r
                SET "ProductionLineId" = l."ProductionLineId",
                    "ProductionStartTime" = l."ProductionStartTime",
                    "ProductionEndTime" = l."ProductionEndTime",
                    "DowntimeHours" = l."DowntimeHours",
                    "ElectricityStartMeter" = l."ElectricityStartMeter",
                    "ElectricityEndMeter" = l."ElectricityEndMeter",
                    "MachineSpeed" = l."MachineSpeed",
                    "FeedDistanceMm" = l."FeedDistanceMm",
                    "CycleTimeSeconds" = l."CycleTimeSeconds"
                FROM (
                    SELECT DISTINCT ON ("ShiftReportId") "ShiftReportId", "ProductionLineId",
                           "ProductionStartTime", "ProductionEndTime", "DowntimeHours",
                           "ElectricityStartMeter", "ElectricityEndMeter",
                           "MachineSpeed", "FeedDistanceMm", "CycleTimeSeconds"
                    FROM "ShiftLines"
                    ORDER BY "ShiftReportId", "Id"
                ) l
                WHERE r."Id" = l."ShiftReportId";
                """);

            migrationBuilder.DropTable(
                name: "ShiftLines");

            migrationBuilder.DropColumn(
                name: "RecordsElectricity",
                table: "ProductionLines");

            migrationBuilder.RenameColumn(
                name: "ShiftLineId",
                table: "ShiftWorkers",
                newName: "ShiftReportId");

            migrationBuilder.RenameIndex(
                name: "ux_shift_workers_line_user",
                table: "ShiftWorkers",
                newName: "ux_shift_workers_report_user");

            migrationBuilder.CreateIndex(
                name: "ix_shift_reports_line_date",
                table: "ShiftReports",
                columns: new[] { "ProductionLineId", "ProductionDate" });

            migrationBuilder.CreateIndex(
                name: "ux_shift_reports_line_shift_date",
                table: "ShiftReports",
                columns: new[] { "ProductionLineId", "ShiftId", "ProductionDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftReports_ProductionLines_ProductionLineId",
                table: "ShiftReports",
                column: "ProductionLineId",
                principalTable: "ProductionLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftWorkers_ShiftReports_ShiftReportId",
                table: "ShiftWorkers",
                column: "ShiftReportId",
                principalTable: "ShiftReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
