using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RollAbsorbencyAndMoreColours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AbsorbencyDryWeight",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AbsorbencyWetWeight",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_roll_tests_absorbency",
                table: "RollTestReports",
                sql: "(\"AbsorbencyDryWeight\" IS NULL AND \"AbsorbencyWetWeight\" IS NULL) OR (\"AbsorbencyDryWeight\" > 0 AND \"AbsorbencyWetWeight\" >= \"AbsorbencyDryWeight\")");
        
            // More colours for the rolls (specification section 19.14), on a system that
            // already has its colours. A new one gets them from the seeder, which only
            // fills an empty table — so nothing is added here when the table is empty.
            // Skipped wherever the name or the letter is already taken.
            migrationBuilder.Sql("""
                INSERT INTO "Colors" ("Name", "Code", "IsActive")
                SELECT n.name, n.code, true
                  FROM (VALUES ('Blue', 'U'), ('Red', 'R'), ('Orange', 'O'), ('Pink', 'P'),
                               ('Purple', 'V'), ('Brown', 'N'), ('Grey', 'E')) AS n(name, code)
                 WHERE EXISTS (SELECT 1 FROM "Colors")
                   AND NOT EXISTS (SELECT 1 FROM "Colors" c WHERE c."Name" = n.name OR c."Code" = n.code);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_roll_tests_absorbency",
                table: "RollTestReports");

            migrationBuilder.DropColumn(
                name: "AbsorbencyDryWeight",
                table: "RollTestReports");

            migrationBuilder.DropColumn(
                name: "AbsorbencyWetWeight",
                table: "RollTestReports");
        }
    }
}
