using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShiftSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftId = table.Column<int>(type: "integer", nullable: false),
                    SupervisorUserId = table.Column<int>(type: "integer", nullable: true),
                    ExtruderStartingShift = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledByUserId = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ShiftReportId = table.Column<int>(type: "integer", nullable: true),
                    NotOpenedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledShifts_ShiftReports_ShiftReportId",
                        column: x => x.ShiftReportId,
                        principalTable: "ShiftReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledShifts_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledShifts_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledShifts_Users_ScheduledByUserId",
                        column: x => x.ScheduledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledShifts_Users_SupervisorUserId",
                        column: x => x.SupervisorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledShiftLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduledShiftId = table.Column<int>(type: "integer", nullable: false),
                    ProductionLineId = table.Column<int>(type: "integer", nullable: false),
                    OperatorUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledShiftLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledShiftLines_ProductionLines_ProductionLineId",
                        column: x => x.ProductionLineId,
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledShiftLines_ScheduledShifts_ScheduledShiftId",
                        column: x => x.ScheduledShiftId,
                        principalTable: "ScheduledShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledShiftLines_Users_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledShiftLines_OperatorUserId",
                table: "ScheduledShiftLines",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledShiftLines_ProductionLineId",
                table: "ScheduledShiftLines",
                column: "ProductionLineId");

            migrationBuilder.CreateIndex(
                name: "ux_scheduled_shift_lines_shift_line",
                table: "ScheduledShiftLines",
                columns: new[] { "ScheduledShiftId", "ProductionLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledShifts_ChangedByUserId",
                table: "ScheduledShifts",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledShifts_ScheduledByUserId",
                table: "ScheduledShifts",
                column: "ScheduledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledShifts_ShiftId",
                table: "ScheduledShifts",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledShifts_SupervisorUserId",
                table: "ScheduledShifts",
                column: "SupervisorUserId");

            migrationBuilder.CreateIndex(
                name: "ux_scheduled_shifts_date_shift",
                table: "ScheduledShifts",
                columns: new[] { "ProductionDate", "ShiftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_scheduled_shifts_report",
                table: "ScheduledShifts",
                column: "ShiftReportId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledShiftLines");

            migrationBuilder.DropTable(
                name: "ScheduledShifts");
        }
    }
}
