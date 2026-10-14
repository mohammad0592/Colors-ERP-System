using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RollTestSavedAsItGoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_roll_tests_absorbency",
                table: "RollTestReports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports");

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessRs",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessRm",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessLs",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessLm",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "PlateWeight",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AbsorbencyDryWeighedAt",
                table: "RollTestReports",
                type: "timestamp with time zone",
                nullable: true);

            // Tests saved before this took both weights at once. The piece went into the
            // water some time before; the save is the nearest moment on record.
            migrationBuilder.Sql(
                """
                UPDATE "RollTestReports" SET "AbsorbencyDryWeighedAt" = "TestedAt"
                WHERE "AbsorbencyDryWeight" IS NOT NULL
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_roll_tests_absorbency",
                table: "RollTestReports",
                sql: "(\"AbsorbencyDryWeight\" IS NULL OR \"AbsorbencyDryWeight\" > 0) AND (\"AbsorbencyWetWeight\" IS NULL OR \"AbsorbencyWetWeight\" >= \"AbsorbencyDryWeight\")");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports",
                sql: "\"PlateWeight\" IS NULL OR \"PlateWeight\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_roll_tests_absorbency",
                table: "RollTestReports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports");

            migrationBuilder.DropColumn(
                name: "AbsorbencyDryWeighedAt",
                table: "RollTestReports");

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessRs",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessRm",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessLs",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ThicknessLm",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PlateWeight",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,3)",
                oldPrecision: 9,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_roll_tests_absorbency",
                table: "RollTestReports",
                sql: "(\"AbsorbencyDryWeight\" IS NULL AND \"AbsorbencyWetWeight\" IS NULL) OR (\"AbsorbencyDryWeight\" > 0 AND \"AbsorbencyWetWeight\" >= \"AbsorbencyDryWeight\")");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports",
                sql: "\"PlateWeight\" > 0");
        }
    }
}
