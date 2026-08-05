using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "pallet_number_seq");

            migrationBuilder.CreateTable(
                name: "WoodenPallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PalletNumber = table.Column<int>(type: "integer", nullable: false),
                    ShiftLineId = table.Column<int>(type: "integer", nullable: false),
                    ColorId = table.Column<int>(type: "integer", nullable: true),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WoodenPallets", x => x.Id);
                    table.CheckConstraint("ck_pallets_colour_and_product_together", "(\"ColorId\" IS NULL) = (\"ProductId\" IS NULL)");
                    table.CheckConstraint("ck_pallets_dates_in_order", "(\"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"CreatedAt\") AND (\"ShippedAt\" IS NULL OR \"CompletedAt\" IS NOT NULL) AND (\"ShippedAt\" IS NULL OR \"ShippedAt\" >= \"CompletedAt\")");
                    table.ForeignKey(
                        name: "FK_WoodenPallets_Colors_ColorId",
                        column: x => x.ColorId,
                        principalTable: "Colors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WoodenPallets_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WoodenPallets_ShiftLines_ShiftLineId",
                        column: x => x.ShiftLineId,
                        principalTable: "ShiftLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WoodenPallets_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BagPalletAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProducedBagId = table.Column<int>(type: "integer", nullable: false),
                    WoodenPalletId = table.Column<int>(type: "integer", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReversedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BagPalletAssignments", x => x.Id);
                    table.CheckConstraint("ck_bag_pallet_reversal_complete", "(\"ReversedAt\" IS NULL AND \"ReversedByUserId\" IS NULL AND \"ReversalReason\" IS NULL) OR (\"ReversedAt\" IS NOT NULL AND \"ReversedByUserId\" IS NOT NULL AND \"ReversalReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BagPalletAssignments_ProducedBags_ProducedBagId",
                        column: x => x.ProducedBagId,
                        principalTable: "ProducedBags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BagPalletAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BagPalletAssignments_Users_ReversedByUserId",
                        column: x => x.ReversedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BagPalletAssignments_WoodenPallets_WoodenPalletId",
                        column: x => x.WoodenPalletId,
                        principalTable: "WoodenPallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BagPalletAssignments_AssignedByUserId",
                table: "BagPalletAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BagPalletAssignments_ReversedByUserId",
                table: "BagPalletAssignments",
                column: "ReversedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_bag_pallet_pallet",
                table: "BagPalletAssignments",
                column: "WoodenPalletId");

            migrationBuilder.CreateIndex(
                name: "ux_bag_pallet_bag",
                table: "BagPalletAssignments",
                column: "ProducedBagId",
                unique: true,
                filter: "\"ReversedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WoodenPallets_ColorId",
                table: "WoodenPallets",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodenPallets_CreatedByUserId",
                table: "WoodenPallets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodenPallets_ProductId",
                table: "WoodenPallets",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_pallets_shift_line",
                table: "WoodenPallets",
                column: "ShiftLineId");

            migrationBuilder.CreateIndex(
                name: "ux_pallets_number",
                table: "WoodenPallets",
                column: "PalletNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BagPalletAssignments");

            migrationBuilder.DropTable(
                name: "WoodenPallets");

            migrationBuilder.DropSequence(
                name: "pallet_number_seq");
        }
    }
}
