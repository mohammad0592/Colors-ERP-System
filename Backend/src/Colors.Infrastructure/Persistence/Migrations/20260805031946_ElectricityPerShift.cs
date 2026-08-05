using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The electricity reading moves from the line up to the shift.
    ///
    /// The factory has a single meter for the whole building, so a reading per line
    /// recorded the same meter two or three times over on a day when several lines
    /// ran, and any total was a multiple of the truth. `RecordsElectricity` on the
    /// production line existed only to hedge that question, and the factory has now
    /// answered it, so the flag goes too.
    ///
    /// The scaffolded version dropped the line columns before moving anything. The
    /// order here copies the readings up first.
    /// </summary>
    public partial class ElectricityPerShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityStartMeter",
                table: "ShiftReports",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityEndMeter",
                table: "ShiftReports",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            // One line's pair of readings, kept together rather than taking the lowest
            // start and the highest end from different rows — that would invent a
            // consumption nobody recorded. Where several lines carry the same meter
            // written down more than once, the largest span is the fullest record.
            migrationBuilder.Sql(
                """
                UPDATE "ShiftReports" r
                SET "ElectricityStartMeter" = l."ElectricityStartMeter",
                    "ElectricityEndMeter"   = l."ElectricityEndMeter"
                FROM (
                    SELECT DISTINCT ON ("ShiftReportId")
                           "ShiftReportId", "ElectricityStartMeter", "ElectricityEndMeter"
                    FROM "ShiftLines"
                    WHERE "ElectricityStartMeter" IS NOT NULL
                       OR "ElectricityEndMeter" IS NOT NULL
                    ORDER BY "ShiftReportId",
                             (COALESCE("ElectricityEndMeter", 0) - COALESCE("ElectricityStartMeter", 0)) DESC,
                             "Id"
                ) l
                WHERE r."Id" = l."ShiftReportId";
                """);

            migrationBuilder.DropColumn(
                name: "ElectricityEndMeter",
                table: "ShiftLines");

            migrationBuilder.DropColumn(
                name: "ElectricityStartMeter",
                table: "ShiftLines");

            migrationBuilder.DropColumn(
                name: "RecordsElectricity",
                table: "ProductionLines");
        }

        /// <summary>
        /// Going back puts the shift's reading on its first line, and ticks every line
        /// as having a meter — which is what the flag meant before the factory told us
        /// there is only one.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityStartMeter",
                table: "ShiftLines",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityEndMeter",
                table: "ShiftLines",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecordsElectricity",
                table: "ProductionLines",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                UPDATE "ShiftLines" sl
                SET "ElectricityStartMeter" = r."ElectricityStartMeter",
                    "ElectricityEndMeter"   = r."ElectricityEndMeter"
                FROM "ShiftReports" r
                WHERE r."Id" = sl."ShiftReportId"
                  AND sl."Id" = (
                      SELECT min("Id") FROM "ShiftLines" WHERE "ShiftReportId" = r."Id"
                  );
                """);

            migrationBuilder.DropColumn(
                name: "ElectricityStartMeter",
                table: "ShiftReports");

            migrationBuilder.DropColumn(
                name: "ElectricityEndMeter",
                table: "ShiftReports");
        }
    }
}
