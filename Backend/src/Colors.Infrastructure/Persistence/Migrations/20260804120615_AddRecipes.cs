using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeFamilies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductTypeId = table.Column<int>(type: "integer", nullable: false),
                    UsesRecycle = table.Column<bool>(type: "boolean", nullable: false),
                    IsAbsorbent = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeFamilies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeFamilies_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeNumber = table.Column<int>(type: "integer", nullable: false),
                    RecipeFamilyId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeVersions_RecipeFamilies_RecipeFamilyId",
                        column: x => x.RecipeFamilyId,
                        principalTable: "RecipeFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeVersionId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    IsBaseResin = table.Column<bool>(type: "boolean", nullable: false),
                    TargetPercentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    MinPercentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    MaxPercentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeFamilies_ProductTypeId",
                table: "RecipeFamilies",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "ux_recipe_families_name",
                table: "RecipeFamilies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_MaterialId",
                table: "RecipeIngredients",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "ux_recipe_ingredients_version_material",
                table: "RecipeIngredients",
                columns: new[] { "RecipeVersionId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_CreatedByUserId",
                table: "RecipeVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ux_recipe_versions_family_version",
                table: "RecipeVersions",
                columns: new[] { "RecipeFamilyId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_recipe_versions_number",
                table: "RecipeVersions",
                column: "RecipeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_recipe_versions_one_current_per_family",
                table: "RecipeVersions",
                column: "RecipeFamilyId",
                unique: true,
                filter: "\"Status\" = 'Current'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "RecipeVersions");

            migrationBuilder.DropTable(
                name: "RecipeFamilies");
        }
    }
}
