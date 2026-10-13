using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RollsAreWeighedAndNumberedByTheFactory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_rolls_date_serial",
                table: "Rolls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports");

            migrationBuilder.RenameColumn(
                name: "DailySerial",
                table: "Rolls",
                newName: "RollNumber");

            migrationBuilder.AlterColumn<string>(
                name: "RollCode",
                table: "Rolls",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                table: "Rolls",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MeasuredAt",
                table: "Rolls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeasuredByUserId",
                table: "Rolls",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "Rolls",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RollNumberCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    NextNumber = table.Column<int>(type: "integer", nullable: false),
                    LastResetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastResetByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RollNumberCounters", x => x.Id);
                    table.CheckConstraint("ck_roll_number_positive", "\"NextNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_RollNumberCounters_Users_LastResetByUserId",
                        column: x => x.LastResetByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rolls_MeasuredByUserId",
                table: "Rolls",
                column: "MeasuredByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rolls_measured_positive",
                table: "Rolls",
                sql: "(\"Weight\" IS NULL OR \"Weight\" > 0) AND (\"Length\" IS NULL OR \"Length\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports",
                sql: "\"PlateWeight\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_RollNumberCounters_LastResetByUserId",
                table: "RollNumberCounters",
                column: "LastResetByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rolls_Users_MeasuredByUserId",
                table: "Rolls",
                column: "MeasuredByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The weight and length move from the test to the roll (specification section
            // 19.12). Copied before the old columns go, with the test's author and time as
            // the measuring, since that is who weighed it and when.
            migrationBuilder.Sql("""
                UPDATE "Rolls" r
                   SET "Weight" = t."Weight",
                       "Length" = t."Length",
                       "MeasuredByUserId" = t."TestedByUserId",
                       "MeasuredAt" = t."TestedAt"
                  FROM "RollTestReports" t
                 WHERE t."RollId" = r."Id";
                """);

            migrationBuilder.DropColumn(
                name: "Length",
                table: "RollTestReports");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "RollTestReports");

            // The running roll number carries on from the last day's count rather than
            // starting the factory over at 01 the moment this is installed.
            migrationBuilder.Sql("""
                INSERT INTO "RollNumberCounters" ("Id", "NextNumber")
                SELECT 1, COALESCE(MAX(r."RollNumber"), 0) + 1
                  FROM "Rolls" r
                 WHERE r."ProductionDate" = (SELECT MAX("ProductionDate") FROM "Rolls");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rolls_Users_MeasuredByUserId",
                table: "Rolls");

            migrationBuilder.DropTable(
                name: "RollNumberCounters");

            migrationBuilder.DropIndex(
                name: "IX_Rolls_MeasuredByUserId",
                table: "Rolls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rolls_measured_positive",
                table: "Rolls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports");

            migrationBuilder.DropColumn(
                name: "Length",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "MeasuredAt",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "MeasuredByUserId",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "Rolls");

            migrationBuilder.RenameColumn(
                name: "RollNumber",
                table: "Rolls",
                newName: "DailySerial");

            migrationBuilder.AlterColumn<string>(
                name: "RollCode",
                table: "Rolls",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "RollTestReports",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ux_rolls_date_serial",
                table: "Rolls",
                columns: new[] { "ProductionDate", "DailySerial" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_roll_tests_positive",
                table: "RollTestReports",
                sql: "\"Weight\" > 0 AND \"Length\" > 0 AND \"PlateWeight\" > 0");
        }
    }
}
