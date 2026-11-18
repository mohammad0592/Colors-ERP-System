using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One role a line (specification section 19.31). The Extruder Test Person becomes the
    /// Extruder Operator; the Thermo Test Person and the Packaging Operator become the
    /// Thermo Operator.
    ///
    /// Everyone holding an old role is given the new one, and every shift record naming
    /// an old role as a job somebody did names the new one instead — then the old roles
    /// go. Data only: the tables do not change.
    /// </summary>
    public partial class MergeLineRoles : Migration
    {
        private static readonly (string Old, string Into)[] Merges =
        [
            ("ExtruderTestPerson", "ExtruderOperator"),
            ("ThermoTestPerson", "ThermoOperator"),
            ("PackagingOperator", "ThermoOperator"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (old, into) in Merges)
            {
                // ON CONFLICT DO NOTHING: a man who already held both keeps one row, not
                // two — the keys would refuse the second anyway, and the whole migration
                // with it.
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO "UserRoles" ("UserId", "RoleId")
                    SELECT ur."UserId", merged."Id"
                    FROM "UserRoles" ur
                    JOIN "Roles" gone ON gone."Id" = ur."RoleId" AND gone."Name" = '{old}'
                    JOIN "Roles" merged ON merged."Name" = '{into}'
                    ON CONFLICT DO NOTHING;

                    INSERT INTO "ShiftWorkerRoles" ("ShiftWorkerId", "RoleId")
                    SELECT swr."ShiftWorkerId", merged."Id"
                    FROM "ShiftWorkerRoles" swr
                    JOIN "Roles" gone ON gone."Id" = swr."RoleId" AND gone."Name" = '{old}'
                    JOIN "Roles" merged ON merged."Name" = '{into}'
                    ON CONFLICT DO NOTHING;

                    DELETE FROM "ShiftWorkerRoles"
                    WHERE "RoleId" IN (SELECT "Id" FROM "Roles" WHERE "Name" = '{old}');

                    DELETE FROM "UserRoles"
                    WHERE "RoleId" IN (SELECT "Id" FROM "Roles" WHERE "Name" = '{old}');

                    DELETE FROM "RoleClaims"
                    WHERE "RoleId" IN (SELECT "Id" FROM "Roles" WHERE "Name" = '{old}');

                    DELETE FROM "Roles" WHERE "Name" = '{old}';
                    """);
            }

            // The seeder only creates roles that are missing, so the descriptions shown in
            // Users are brought up to date here.
            migrationBuilder.Sql(
                """
                UPDATE "Roles" SET "Description" = 'Opening, closing and planning shifts, inventory adjustments, approving corrections.'
                WHERE "Name" = 'Supervisor';

                UPDATE "Roles" SET "Description" = 'Line 1 — batches and rolls, weighing them and testing them.'
                WHERE "Name" = 'ExtruderOperator';

                UPDATE "Roles" SET "Description" = 'Line 2 — forming rolls, counting and testing what came out, pallets and packaging.'
                WHERE "Name" = 'ThermoOperator';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not undone. Who held which of the old roles is gone once they are merged, and
            // guessing it back would hand people jobs they never had. A database that has
            // to go back restores the backup Migrate.ps1 takes first.
        }
    }
}
