using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecyclerRecordsOnlyItsOutput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scrap weight goes, and it is not coming back. The factory keeps scrap
            // in two silos and draws it out to be ground, so a shift's scrap is never on
            // a scale — the column asked for a measurement that does not exist
            // (specification section 11).
            migrationBuilder.DropCheckConstraint(
                name: "ck_recycler_weights",
                table: "RecyclerProductions");

            migrationBuilder.DropColumn(
                name: "ScrapWeight",
                table: "RecyclerProductions");

            // The old rule allowed a record with scrap weighed in and nothing ground
            // out. With the scrap gone such a row says nothing at all, and it never
            // moved any stock, so there is nothing to unwind. Removed before the
            // constraint, which would otherwise refuse to build.
            migrationBuilder.Sql(
                """DELETE FROM "RecyclerProductions" WHERE "RecycledMaterialWeight" <= 0;""");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recycler_weight_positive",
                table: "RecyclerProductions",
                sql: "\"RecycledMaterialWeight\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_recycler_weight_positive",
                table: "RecyclerProductions");

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapWeight",
                table: "RecyclerProductions",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_recycler_weights",
                table: "RecyclerProductions",
                sql: "\"ScrapWeight\" >= 0 AND \"RecycledMaterialWeight\" >= 0 AND (\"ScrapWeight\" > 0 OR \"RecycledMaterialWeight\" > 0)");
        }
    }
}
