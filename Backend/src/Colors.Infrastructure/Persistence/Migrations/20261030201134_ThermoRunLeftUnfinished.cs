using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThermoRunLeftUnfinished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_thermo_productions_roll",
                table: "ThermoProductions");

            migrationBuilder.AddColumn<string>(
                name: "UnfinishedReason",
                table: "ThermoProductions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_thermo_productions_roll",
                table: "ThermoProductions",
                column: "RollId",
                unique: true,
                filter: "\"UnfinishedReason\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_thermo_productions_roll_in_machine",
                table: "ThermoProductions",
                column: "RollId",
                unique: true,
                filter: "\"FinishedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_thermo_unfinished_is_out",
                table: "ThermoProductions",
                sql: "\"UnfinishedReason\" IS NULL OR \"FinishedAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_thermo_productions_roll",
                table: "ThermoProductions");

            migrationBuilder.DropIndex(
                name: "ux_thermo_productions_roll_in_machine",
                table: "ThermoProductions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_thermo_unfinished_is_out",
                table: "ThermoProductions");

            migrationBuilder.DropColumn(
                name: "UnfinishedReason",
                table: "ThermoProductions");

            migrationBuilder.CreateIndex(
                name: "ux_thermo_productions_roll",
                table: "ThermoProductions",
                column: "RollId",
                unique: true);
        }
    }
}
