using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameRoleInShiftAndAddMachineSettingsFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftWorkers_Roles_RoleId",
                table: "ShiftWorkers");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "ShiftWorkers",
                newName: "RoleInShiftId");

            migrationBuilder.RenameIndex(
                name: "IX_ShiftWorkers_RoleId",
                table: "ShiftWorkers",
                newName: "IX_ShiftWorkers_RoleInShiftId");

            migrationBuilder.AddColumn<bool>(
                name: "RecordsMachineSettings",
                table: "ProductionLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The seeder only inserts lines that are missing, so a database that already
            // has its three lines would never pick the flag up. Set it here for the
            // thermo line, which is the only one the factory records settings for.
            migrationBuilder.Sql(
                """UPDATE "ProductionLines" SET "RecordsMachineSettings" = true WHERE "Name" = 'Thermo';""");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftWorkers_Roles_RoleInShiftId",
                table: "ShiftWorkers",
                column: "RoleInShiftId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftWorkers_Roles_RoleInShiftId",
                table: "ShiftWorkers");

            migrationBuilder.DropColumn(
                name: "RecordsMachineSettings",
                table: "ProductionLines");

            migrationBuilder.RenameColumn(
                name: "RoleInShiftId",
                table: "ShiftWorkers",
                newName: "RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_ShiftWorkers_RoleInShiftId",
                table: "ShiftWorkers",
                newName: "IX_ShiftWorkers_RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftWorkers_Roles_RoleId",
                table: "ShiftWorkers",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
