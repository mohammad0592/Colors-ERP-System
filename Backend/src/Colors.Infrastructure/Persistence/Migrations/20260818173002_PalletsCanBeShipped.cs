using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PalletsCanBeShipped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShippedByUserId",
                table: "WoodenPallets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingReversalReason",
                table: "WoodenPallets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ShippingReversedAt",
                table: "WoodenPallets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingReversedByUserId",
                table: "WoodenPallets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WoodenPallets_ShippedByUserId",
                table: "WoodenPallets",
                column: "ShippedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodenPallets_ShippingReversedByUserId",
                table: "WoodenPallets",
                column: "ShippingReversedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pallets_shipped_together",
                table: "WoodenPallets",
                sql: "(\"ShippedAt\" IS NULL) = (\"ShippedByUserId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pallets_shipping_reversal_complete",
                table: "WoodenPallets",
                sql: "(\"ShippingReversedAt\" IS NULL) = (\"ShippingReversedByUserId\" IS NULL) AND (\"ShippingReversedAt\" IS NULL) = (\"ShippingReversalReason\" IS NULL) AND (\"ShippingReversedAt\" IS NULL OR \"ShippedAt\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_WoodenPallets_Users_ShippedByUserId",
                table: "WoodenPallets",
                column: "ShippedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WoodenPallets_Users_ShippingReversedByUserId",
                table: "WoodenPallets",
                column: "ShippingReversedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WoodenPallets_Users_ShippedByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropForeignKey(
                name: "FK_WoodenPallets_Users_ShippingReversedByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropIndex(
                name: "IX_WoodenPallets_ShippedByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropIndex(
                name: "IX_WoodenPallets_ShippingReversedByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pallets_shipped_together",
                table: "WoodenPallets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pallets_shipping_reversal_complete",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "ShippedByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "ShippingReversalReason",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "ShippingReversedAt",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "ShippingReversedByUserId",
                table: "WoodenPallets");
        }
    }
}
