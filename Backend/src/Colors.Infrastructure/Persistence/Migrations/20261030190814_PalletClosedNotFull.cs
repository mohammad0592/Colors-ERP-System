using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PalletClosedNotFull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pallets_dates_in_order",
                table: "WoodenPallets");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "WoodenPallets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedByUserId",
                table: "WoodenPallets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WoodenPallets_ClosedByUserId",
                table: "WoodenPallets",
                column: "ClosedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pallets_closed_together",
                table: "WoodenPallets",
                sql: "(\"ClosedAt\" IS NULL) = (\"ClosedByUserId\" IS NULL) AND (\"ClosedAt\" IS NULL OR (\"CompletedAt\" IS NULL AND \"CancelledAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pallets_dates_in_order",
                table: "WoodenPallets",
                sql: "(\"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"CreatedAt\") AND (\"ClosedAt\" IS NULL OR \"ClosedAt\" >= \"CreatedAt\") AND (\"ShippedAt\" IS NULL OR \"CompletedAt\" IS NOT NULL OR \"ClosedAt\" IS NOT NULL) AND (\"ShippedAt\" IS NULL OR \"ShippedAt\" >= COALESCE(\"CompletedAt\", \"ClosedAt\"))");

            migrationBuilder.AddForeignKey(
                name: "FK_WoodenPallets_Users_ClosedByUserId",
                table: "WoodenPallets",
                column: "ClosedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WoodenPallets_Users_ClosedByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropIndex(
                name: "IX_WoodenPallets_ClosedByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pallets_closed_together",
                table: "WoodenPallets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pallets_dates_in_order",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "WoodenPallets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pallets_dates_in_order",
                table: "WoodenPallets",
                sql: "(\"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"CreatedAt\") AND (\"ShippedAt\" IS NULL OR \"CompletedAt\" IS NOT NULL) AND (\"ShippedAt\" IS NULL OR \"ShippedAt\" >= \"CompletedAt\")");
        }
    }
}
