using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One open shift at a time (specification section 2).
    /// </summary>
    public partial class SingleOpenShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A database that already has two shifts open cannot take the index, so the
            // older ones are closed first — newest kept, because that is the one the
            // factory is actually working.
            //
            // The reason goes in Notes for the same reason a reopening's does: there is
            // no audit log yet, and a shift that changed state with no explanation is
            // worse than a long note.
            migrationBuilder.Sql(
                """
                WITH keep AS (
                    SELECT "Id" FROM "ShiftReports"
                    WHERE "Status" = 'Open'
                    ORDER BY "OpenedAt" DESC, "Id" DESC
                    LIMIT 1
                )
                UPDATE "ShiftReports"
                SET "Status" = 'Closed',
                    "ClosedAt" = NOW(),
                    "Notes" = LEFT(
                        COALESCE("Notes" || E'\n', '')
                        || '[Closed by upgrade] The factory works one shift at a time; '
                        || 'this one was left open while another was running.', 1000)
                WHERE "Status" = 'Open'
                  AND "Id" NOT IN (SELECT "Id" FROM keep);
                """);

            // The rule itself. Indexing a constant means every open row collides with
            // every other open row, so only one can exist — which is what stops two
            // supervisors on two tablets both opening a shift in the same moment.
            //
            // Written as SQL because EF cannot express an index on an expression. It
            // cannot see this one either, so it will never scaffold a drop for it.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "ux_shift_reports_single_open"
                    ON "ShiftReports" ((TRUE))
                    WHERE "Status" = 'Open';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the index comes back off. The shifts this closed stay closed —
            // reopening them automatically would put the database straight back into
            // the state the factory says is impossible.
            migrationBuilder.Sql(
                """DROP INDEX IF EXISTS "ux_shift_reports_single_open";""");
        }
    }
}
