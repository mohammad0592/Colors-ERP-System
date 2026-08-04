using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "recipe_number_seq");

            // Databases seeded before the sequence existed already hold recipe
            // numbers. Start the sequence after the highest of them, so no number is
            // ever handed out twice.
            migrationBuilder.Sql(
                """
                SELECT setval(
                    'recipe_number_seq',
                    COALESCE((SELECT MAX("RecipeNumber") FROM "RecipeVersions"), 0) + 1,
                    false);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "recipe_number_seq");
        }
    }
}
