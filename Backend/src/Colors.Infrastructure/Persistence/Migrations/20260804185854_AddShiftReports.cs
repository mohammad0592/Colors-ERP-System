using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductionLineId = table.Column<int>(type: "integer", nullable: false),
                    ShiftId = table.Column<int>(type: "integer", nullable: false),
                    ProductionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SupervisorUserId = table.Column<int>(type: "integer", nullable: true),
                    ProductionStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ProductionEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DowntimeHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    ElectricityStartMeter = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    ElectricityEndMeter = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    MachineSpeed = table.Column<int>(type: "integer", nullable: true),
                    FeedDistanceMm = table.Column<int>(type: "integer", nullable: true),
                    CycleTimeSeconds = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OpenedByUserId = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftReports_ProductionLines_ProductionLineId",
                        column: x => x.ProductionLineId,
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftReports_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftReports_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftReports_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftReports_Users_SupervisorUserId",
                        column: x => x.SupervisorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftWorkers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftReportId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    IsTrainee = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftWorkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftWorkers_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftWorkers_ShiftReports_ShiftReportId",
                        column: x => x.ShiftReportId,
                        principalTable: "ShiftReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftWorkers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_ClosedByUserId",
                table: "ShiftReports",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_OpenedByUserId",
                table: "ShiftReports",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_ShiftId",
                table: "ShiftReports",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_SupervisorUserId",
                table: "ShiftReports",
                column: "SupervisorUserId");

            migrationBuilder.CreateIndex(
                name: "ix_shift_reports_line_date",
                table: "ShiftReports",
                columns: new[] { "ProductionLineId", "ProductionDate" });

            migrationBuilder.CreateIndex(
                name: "ux_shift_reports_line_shift_date",
                table: "ShiftReports",
                columns: new[] { "ProductionLineId", "ShiftId", "ProductionDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftWorkers_RoleId",
                table: "ShiftWorkers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftWorkers_UserId",
                table: "ShiftWorkers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ux_shift_workers_report_user",
                table: "ShiftWorkers",
                columns: new[] { "ShiftReportId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftWorkers");

            migrationBuilder.DropTable(
                name: "ShiftReports");
        }
    }
}
