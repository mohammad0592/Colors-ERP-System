using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShiftLineOperatorsAndMeterAuthors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ElectricityEndRecordedAt",
                table: "ShiftReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ElectricityEndRecordedByUserId",
                table: "ShiftReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ElectricityStartRecordedAt",
                table: "ShiftReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ElectricityStartRecordedByUserId",
                table: "ShiftReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ShiftLines",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperatorUserId",
                table: "ShiftLines",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_ElectricityEndRecordedByUserId",
                table: "ShiftReports",
                column: "ElectricityEndRecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_ElectricityStartRecordedByUserId",
                table: "ShiftReports",
                column: "ElectricityStartRecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftLines_OperatorUserId",
                table: "ShiftLines",
                column: "OperatorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftLines_Users_OperatorUserId",
                table: "ShiftLines",
                column: "OperatorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftReports_Users_ElectricityEndRecordedByUserId",
                table: "ShiftReports",
                column: "ElectricityEndRecordedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftReports_Users_ElectricityStartRecordedByUserId",
                table: "ShiftReports",
                column: "ElectricityStartRecordedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftLines_Users_OperatorUserId",
                table: "ShiftLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftReports_Users_ElectricityEndRecordedByUserId",
                table: "ShiftReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftReports_Users_ElectricityStartRecordedByUserId",
                table: "ShiftReports");

            migrationBuilder.DropIndex(
                name: "IX_ShiftReports_ElectricityEndRecordedByUserId",
                table: "ShiftReports");

            migrationBuilder.DropIndex(
                name: "IX_ShiftReports_ElectricityStartRecordedByUserId",
                table: "ShiftReports");

            migrationBuilder.DropIndex(
                name: "IX_ShiftLines_OperatorUserId",
                table: "ShiftLines");

            migrationBuilder.DropColumn(
                name: "ElectricityEndRecordedAt",
                table: "ShiftReports");

            migrationBuilder.DropColumn(
                name: "ElectricityEndRecordedByUserId",
                table: "ShiftReports");

            migrationBuilder.DropColumn(
                name: "ElectricityStartRecordedAt",
                table: "ShiftReports");

            migrationBuilder.DropColumn(
                name: "ElectricityStartRecordedByUserId",
                table: "ShiftReports");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ShiftLines");

            migrationBuilder.DropColumn(
                name: "OperatorUserId",
                table: "ShiftLines");
        }
    }
}
