using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductsAreTheMoulds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Moulds_MouldId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftLines_Moulds_MouldId",
                table: "ShiftLines");

            migrationBuilder.DropTable(
                name: "Moulds");

            migrationBuilder.DropIndex(
                name: "IX_ShiftLines_MouldId",
                table: "ShiftLines");

            migrationBuilder.DropIndex(
                name: "ux_products_mould_absorbent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MouldId",
                table: "ShiftLines");

            migrationBuilder.DropColumn(
                name: "MouldId",
                table: "Products");

            // The product is the mould now (specification section 19.9), and the factory
            // named its eight products. The four plates were already right under other
            // names, so they are renamed and keep whatever was made as them.
            migrationBuilder.Sql("""
                UPDATE "Products" p SET "Name" = n.new_name
                  FROM (VALUES ('Big Plate — Normal', 'Normal Big Plate'),
                               ('Small Plate — Normal', 'Normal Small Plate'),
                               ('Big Plate — Absorbent', 'Absorbent Big Plate'),
                               ('Small Plate — Absorbent', 'Absorbent Small Plate'))
                       AS n(old_name, new_name)
                 WHERE p."Name" = n.old_name
                   AND NOT EXISTS (SELECT 1 FROM "Products" q WHERE q."Name" = n.new_name);
                """);

            // The three old boxes are not among the eight. Deleted where nothing was ever
            // made as them; retired where something was, so those bags keep their name.
            migrationBuilder.Sql("""
                DELETE FROM "Products" p
                 WHERE p."Name" IN ('Large Meal Box', 'Small Meal Box', '3-Compartment Clamshell')
                   AND NOT EXISTS (SELECT 1 FROM "Rolls" r WHERE r."ProductId" = p."Id")
                   AND NOT EXISTS (SELECT 1 FROM "ProducedBags" b WHERE b."ProductId" = p."Id")
                   AND NOT EXISTS (SELECT 1 FROM "ThermoTestReports" t WHERE t."ProductId" = p."Id")
                   AND NOT EXISTS (SELECT 1 FROM "WoodenPallets" w WHERE w."ProductId" = p."Id");

                UPDATE "Products" SET "IsActive" = false
                 WHERE "Name" IN ('Large Meal Box', 'Small Meal Box', '3-Compartment Clamshell');
                """);

            // The types follow: the meal boxes were the burger boxes and the clamshell was
            // the lunch box, by the factory's own mould names.
            migrationBuilder.Sql("""
                UPDATE "ProductTypes" SET "Name" = 'Burger Box'
                 WHERE "Name" = 'Meal Box'
                   AND NOT EXISTS (SELECT 1 FROM "ProductTypes" WHERE "Name" = 'Burger Box');

                UPDATE "ProductTypes" SET "Name" = 'Lunch Box'
                 WHERE "Id" = (SELECT min("Id") FROM "ProductTypes" WHERE "Name" IN ('Clamshell', 'Clam Shell'))
                   AND NOT EXISTS (SELECT 1 FROM "ProductTypes" WHERE "Name" = 'Lunch Box');

                INSERT INTO "ProductTypes" ("Name", "IsActive")
                SELECT t.name, true
                  FROM (VALUES ('Plate'), ('Lunch Box'), ('Burger Box')) AS t(name)
                 WHERE EXISTS (SELECT 1 FROM "ProductTypes")
                   AND NOT EXISTS (SELECT 1 FROM "ProductTypes" x WHERE x."Name" = t.name);
                """);

            // Any of the eight still missing is added, made from its main recipe. Only on a
            // database that already had products: a new one is filled by the seeder, which
            // runs after this and finds the table empty.
            migrationBuilder.Sql("""
                INSERT INTO "Products" ("Name", "ProductTypeId", "RecipeFamilyId", "IsAbsorbent",
                                        "PiecesPerBag", "SmallBagsPerBag", "LargeBagsPerBag",
                                        "BagsPerPallet", "IsActive")
                SELECT n.name,
                       (SELECT t."Id" FROM "ProductTypes" t WHERE t."Name" = n.type_name),
                       (SELECT f."Id" FROM "RecipeFamilies" f
                         WHERE f."IsActive" AND upper(f."Code") = upper(n.family_code)
                         ORDER BY f."Id" LIMIT 1),
                       n.absorbent, n.pieces, n.small_bags, n.large_bags, n.per_pallet, true
                  FROM (VALUES
                        ('Normal Big Plate',        'Plate',      'N',   false, 500, 2, 1, 15),
                        ('Normal Small Plate',      'Plate',      'N',   false, 500, 2, 1, 15),
                        ('Absorbent Big Plate',     'Plate',      'Abs', true,  500, 2, 1, 15),
                        ('Absorbent Small Plate',   'Plate',      'Abs', true,  500, 2, 1, 15),
                        ('1-Compartment Lunch Box', 'Lunch Box',  'LN',  false, 250, 1, 0, 21),
                        ('3-Compartment Lunch Box', 'Lunch Box',  'LN',  false, 250, 1, 0, 21),
                        ('Large Burger Box',        'Burger Box', 'LN',  false, 250, 1, 0, 21),
                        ('Small Burger Box',        'Burger Box', 'LN',  false, 250, 1, 0, 21))
                       AS n(name, type_name, family_code, absorbent, pieces, small_bags, large_bags, per_pallet)
                 WHERE EXISTS (SELECT 1 FROM "Products")
                   AND EXISTS (SELECT 1 FROM "ProductTypes" t WHERE t."Name" = n.type_name)
                   AND NOT EXISTS (SELECT 1 FROM "Products" p WHERE p."Name" = n.name);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MouldId",
                table: "ShiftLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MouldId",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Moulds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moulds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftLines_MouldId",
                table: "ShiftLines",
                column: "MouldId");

            migrationBuilder.CreateIndex(
                name: "ux_products_mould_absorbent",
                table: "Products",
                columns: new[] { "MouldId", "IsAbsorbent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_moulds_name",
                table: "Moulds",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Moulds_MouldId",
                table: "Products",
                column: "MouldId",
                principalTable: "Moulds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftLines_Moulds_MouldId",
                table: "ShiftLines",
                column: "MouldId",
                principalTable: "Moulds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
