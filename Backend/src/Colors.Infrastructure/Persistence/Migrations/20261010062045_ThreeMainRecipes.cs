using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThreeMainRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Black became an ordinary colour (specification section 5), so the two Black
            // families are retired before the column that marks them goes. Retired, not
            // deleted: the rolls already made point at their recipes and must keep them.
            migrationBuilder.Sql("""
                UPDATE "RecipeFamilies" SET "IsActive" = false WHERE "BlackOnly";
                """);

            // The other two keep their recipes, numbers and rolls, and take the names the
            // factory uses. Only when nobody has already made a family by that name. The
            // third, Lunch Box, is added by the seeder on the next start, with the products
            // linked to each.
            migrationBuilder.Sql("""
                UPDATE "RecipeFamilies"
                   SET "Name" = 'Normal',
                       "Description" = 'Rolls for the normal big and small plates.'
                 WHERE "Name" = 'Normal (Except Black)'
                   AND NOT EXISTS (SELECT 1 FROM "RecipeFamilies" WHERE "Name" = 'Normal');

                UPDATE "RecipeFamilies"
                   SET "Name" = 'Absorbent',
                       "Description" = 'Rolls for the absorbent big and small plates.'
                 WHERE "Name" = 'ABS (Except Black)'
                   AND NOT EXISTS (SELECT 1 FROM "RecipeFamilies" WHERE "Name" = 'Absorbent');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeFamilies_ProductTypes_ProductTypeId",
                table: "RecipeFamilies");

            migrationBuilder.DropIndex(
                name: "IX_RecipeFamilies_ProductTypeId",
                table: "RecipeFamilies");

            migrationBuilder.DropColumn(
                name: "MaxPercentage",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "MinPercentage",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "BlackOnly",
                table: "RecipeFamilies");

            migrationBuilder.DropColumn(
                name: "ProductTypeId",
                table: "RecipeFamilies");

            migrationBuilder.DropColumn(
                name: "UsesRecycle",
                table: "RecipeFamilies");

            migrationBuilder.DropColumn(
                name: "IsBlack",
                table: "Colors");

            migrationBuilder.AddColumn<int>(
                name: "RecipeFamilyId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_RecipeFamilyId",
                table: "Products",
                column: "RecipeFamilyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_RecipeFamilies_RecipeFamilyId",
                table: "Products",
                column: "RecipeFamilyId",
                principalTable: "RecipeFamilies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_RecipeFamilies_RecipeFamilyId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_RecipeFamilyId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RecipeFamilyId",
                table: "Products");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPercentage",
                table: "RecipeIngredients",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinPercentage",
                table: "RecipeIngredients",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "BlackOnly",
                table: "RecipeFamilies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProductTypeId",
                table: "RecipeFamilies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UsesRecycle",
                table: "RecipeFamilies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlack",
                table: "Colors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeFamilies_ProductTypeId",
                table: "RecipeFamilies",
                column: "ProductTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeFamilies_ProductTypes_ProductTypeId",
                table: "RecipeFamilies",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
