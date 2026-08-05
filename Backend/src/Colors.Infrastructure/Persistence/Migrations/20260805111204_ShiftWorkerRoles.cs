using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A worker's job on a shift becomes a list.
    ///
    /// One man commonly does two — the same person runs the extruder and takes its
    /// measurements, and the thermo operator also builds the pallets. A single column
    /// made him pick, and the record then said he ran the extruder and said nothing
    /// about the testing he also did.
    ///
    /// The jobs go in their own table rather than repeating the worker row, so
    /// <c>IsTrainee</c> stays one fact about that man on that shift and cannot
    /// disagree with itself.
    ///
    /// The scaffolded version dropped the old column before the new table existed,
    /// which would have thrown away every job already recorded. The order here creates
    /// the table, copies the jobs across, and drops the column last.
    /// </summary>
    public partial class ShiftWorkerRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftWorkerRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftWorkerId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftWorkerRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftWorkerRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftWorkerRoles_ShiftWorkers_ShiftWorkerId",
                        column: x => x.ShiftWorkerId,
                        principalTable: "ShiftWorkers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftWorkerRoles_RoleId",
                table: "ShiftWorkerRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "ux_shift_worker_roles_worker_role",
                table: "ShiftWorkerRoles",
                columns: new[] { "ShiftWorkerId", "RoleId" },
                unique: true);

            // Every job already recorded becomes the first entry in that worker's list.
            // Workers whose job was never written down simply have none, which is the
            // same as before.
            migrationBuilder.Sql(
                """
                INSERT INTO "ShiftWorkerRoles" ("ShiftWorkerId", "RoleId")
                SELECT "Id", "RoleInShiftId"
                FROM "ShiftWorkers"
                WHERE "RoleInShiftId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftWorkers_Roles_RoleInShiftId",
                table: "ShiftWorkers");

            migrationBuilder.DropIndex(
                name: "IX_ShiftWorkers_RoleInShiftId",
                table: "ShiftWorkers");

            migrationBuilder.DropColumn(
                name: "RoleInShiftId",
                table: "ShiftWorkers");
        }

        /// <summary>
        /// Going back keeps one job per worker — the lowest role id, so the choice is
        /// at least repeatable. A man who did two jobs loses the second, because the
        /// old shape has nowhere to put it.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleInShiftId",
                table: "ShiftWorkers",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "ShiftWorkers" w
                SET "RoleInShiftId" = r."RoleId"
                FROM (
                    SELECT DISTINCT ON ("ShiftWorkerId") "ShiftWorkerId", "RoleId"
                    FROM "ShiftWorkerRoles"
                    ORDER BY "ShiftWorkerId", "RoleId"
                ) r
                WHERE w."Id" = r."ShiftWorkerId";
                """);

            migrationBuilder.DropTable(
                name: "ShiftWorkerRoles");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftWorkers_RoleInShiftId",
                table: "ShiftWorkers",
                column: "RoleInShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftWorkers_Roles_RoleInShiftId",
                table: "ShiftWorkers",
                column: "RoleInShiftId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
