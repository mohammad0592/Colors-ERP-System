using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecipeColourMustAgree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BlackOnly",
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

            // Both arrive false, which would mean no recipe is black-only and no colour
            // is black — the rule would be in the code and never fire. So they are
            // filled in from what the database already knows.
            //
            // The black families are the ones that use recycle. That is not a guess:
            // replacing 35% of the GPPS with recycled material is *why* they can only be
            // made in black, so the existing column identifies them exactly.
            migrationBuilder.Sql(
                """
                UPDATE "RecipeFamilies" SET "BlackOnly" = TRUE WHERE "UsesRecycle";
                """);

            // Colour has no such column to lean on, so this one time the name is used —
            // acceptable in a migration, which is a fix applied once to data that
            // already exists. The running system never looks at a colour's name again,
            // and never at its letter either: Blue starts with B as well.
            migrationBuilder.Sql(
                """
                UPDATE "Colors" SET "IsBlack" = TRUE WHERE LOWER("Name") = 'black';
                """);

            // Rolls already recorded with a recipe and colour that disagree are left
            // exactly as they are. They are what the factory actually produced, or what
            // somebody actually typed, and a migration inventing a different colour for
            // them would replace a known-wrong record with a made-up one. The rule stops
            // new ones; the old ones are a conversation with the factory.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlackOnly",
                table: "RecipeFamilies");

            migrationBuilder.DropColumn(
                name: "IsBlack",
                table: "Colors");
        }
    }
}
