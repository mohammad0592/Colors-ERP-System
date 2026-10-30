using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffWithoutAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "ShiftWorkers",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "StaffMemberId",
                table: "ShiftWorkers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffMembers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftWorkers_StaffMemberId",
                table: "ShiftWorkers",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "ux_shift_workers_line_staff",
                table: "ShiftWorkers",
                columns: new[] { "ShiftLineId", "StaffMemberId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shift_workers_one_person",
                table: "ShiftWorkers",
                sql: "(\"UserId\" IS NULL) <> (\"StaffMemberId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_CreatedByUserId",
                table: "StaffMembers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ux_staff_members_employee_number",
                table: "StaffMembers",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftWorkers_StaffMembers_StaffMemberId",
                table: "ShiftWorkers",
                column: "StaffMemberId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftWorkers_StaffMembers_StaffMemberId",
                table: "ShiftWorkers");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropIndex(
                name: "IX_ShiftWorkers_StaffMemberId",
                table: "ShiftWorkers");

            migrationBuilder.DropIndex(
                name: "ux_shift_workers_line_staff",
                table: "ShiftWorkers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shift_workers_one_person",
                table: "ShiftWorkers");

            migrationBuilder.DropColumn(
                name: "StaffMemberId",
                table: "ShiftWorkers");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "ShiftWorkers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
