using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Corrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CorrectedAt",
                table: "ThermoTestReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorrectedByUserId",
                table: "ThermoTestReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionReason",
                table: "ThermoTestReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovalReason",
                table: "Rolls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAt",
                table: "Rolls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemovedByUserId",
                table: "Rolls",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovalReason",
                table: "ProducedBags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAt",
                table: "ProducedBags",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemovedByUserId",
                table: "ProducedBags",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "MaterialIssueTickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversesMovementId",
                table: "MaterialInventoryMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rolls_removed_together",
                table: "Rolls",
                sql: "(\"RemovedAt\" IS NULL) = (\"RemovedByUserId\" IS NULL) AND (\"RemovedAt\" IS NULL) = (\"RemovalReason\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_produced_bags_removed_together",
                table: "ProducedBags",
                sql: "(\"RemovedAt\" IS NULL) = (\"RemovedByUserId\" IS NULL) AND (\"RemovedAt\" IS NULL) = (\"RemovalReason\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ux_material_movements_reverses",
                table: "MaterialInventoryMovements",
                column: "ReversesMovementId",
                unique: true,
                filter: "\"ReversesMovementId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialInventoryMovements_MaterialInventoryMovements_Rever~",
                table: "MaterialInventoryMovements",
                column: "ReversesMovementId",
                principalTable: "MaterialInventoryMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialInventoryMovements_MaterialInventoryMovements_Rever~",
                table: "MaterialInventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rolls_removed_together",
                table: "Rolls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_produced_bags_removed_together",
                table: "ProducedBags");

            migrationBuilder.DropIndex(
                name: "ux_material_movements_reverses",
                table: "MaterialInventoryMovements");

            migrationBuilder.DropColumn(
                name: "CorrectedAt",
                table: "ThermoTestReports");

            migrationBuilder.DropColumn(
                name: "CorrectedByUserId",
                table: "ThermoTestReports");

            migrationBuilder.DropColumn(
                name: "CorrectionReason",
                table: "ThermoTestReports");

            migrationBuilder.DropColumn(
                name: "RemovalReason",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "RemovedByUserId",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "RemovalReason",
                table: "ProducedBags");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "ProducedBags");

            migrationBuilder.DropColumn(
                name: "RemovedByUserId",
                table: "ProducedBags");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "MaterialIssueTickets");

            migrationBuilder.DropColumn(
                name: "ReversesMovementId",
                table: "MaterialInventoryMovements");
        }
    }
}
