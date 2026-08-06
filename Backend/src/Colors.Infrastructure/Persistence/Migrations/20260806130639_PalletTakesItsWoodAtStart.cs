using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PalletTakesItsWoodAtStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "WoodenPallets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "WoodenPallets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByUserId",
                table: "WoodenPallets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WoodenPallets_CancelledByUserId",
                table: "WoodenPallets",
                column: "CancelledByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pallets_cancelled_together",
                table: "WoodenPallets",
                sql: "(\"CancelledAt\" IS NULL) = (\"CancelledByUserId\" IS NULL) AND (\"CancelledAt\" IS NULL) = (\"CancellationReason\" IS NULL) AND (\"CancelledAt\" IS NULL OR (\"CompletedAt\" IS NULL AND \"ShippedAt\" IS NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_WoodenPallets_Users_CancelledByUserId",
                table: "WoodenPallets",
                column: "CancelledByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ----------------------------------------------------------------------
            // The pallets built before this rule existed.
            //
            // Under the old rule the wood came out at shift close, and only for a
            // pallet that had been finished — so a pallet left half-built when its
            // shift ended was never counted by anybody. Its shift had passed, and the
            // next shift's count only looks at its own pallets.
            //
            // Each one gets the movement it should always have had. They are paid for
            // by an opening receive of the same size: those pallets of wood really were
            // in the store, nobody ever entered them, and without it the ledger would
            // have to go below nothing to record what plainly happened.
            //
            // The balance is left exactly where it is. This adds the history, not a
            // different answer.
            // ----------------------------------------------------------------------
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    wood_id   INT;
                    receive   INT;
                    consume   INT;
                    who       INT;
                    missing   INT;
                BEGIN
                    SELECT "Id" INTO wood_id FROM "Materials"
                    WHERE "CountedAs" = 'WoodenPallet' LIMIT 1;

                    IF wood_id IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT COUNT(*) INTO missing FROM "WoodenPallets" p
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "MaterialInventoryMovements" m
                        WHERE m."MaterialId" = wood_id
                          AND m."Notes" = 'Pallet ' || p."PalletNumber" || ' started, recorded late');

                    IF missing = 0 THEN
                        RETURN;
                    END IF;

                    SELECT "Id" INTO receive FROM "MovementTypes" WHERE "Name" = 'Receive';
                    SELECT "Id" INTO consume FROM "MovementTypes" WHERE "Name" = 'Packaging Consumption';
                    SELECT "Id" INTO who FROM "Users" ORDER BY "Id" LIMIT 1;

                    IF receive IS NULL OR consume IS NULL OR who IS NULL THEN
                        RETURN;
                    END IF;

                    INSERT INTO "MaterialInventoryMovements"
                        ("MaterialId", "MovementTypeId", "Quantity", "UserId", "MovementDate", "Notes")
                    VALUES (wood_id, receive, missing, who, NOW(),
                            'Opening count that was never entered: the wooden pallets already used');

                    INSERT INTO "MaterialInventoryMovements"
                        ("MaterialId", "MovementTypeId", "Quantity", "UserId", "MovementDate", "Notes")
                    SELECT wood_id, consume, 1, who, NOW(),
                           'Pallet ' || p."PalletNumber" || ' started, recorded late'
                    FROM "WoodenPallets" p
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "MaterialInventoryMovements" m
                        WHERE m."MaterialId" = wood_id
                          AND m."Notes" = 'Pallet ' || p."PalletNumber" || ' started, recorded late');

                    -- The two cancel out, so the figure on the shelf does not move.
                    INSERT INTO "MaterialInventory" ("MaterialId", "CurrentQuantity", "LastUpdated")
                    VALUES (wood_id, 0, NOW())
                    ON CONFLICT ("MaterialId") DO NOTHING;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WoodenPallets_Users_CancelledByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropIndex(
                name: "IX_WoodenPallets_CancelledByUserId",
                table: "WoodenPallets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pallets_cancelled_together",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "WoodenPallets");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "WoodenPallets");
        }
    }
}
