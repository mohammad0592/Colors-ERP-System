using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Only raw material goes out on an issue ticket. Packaging goes to the bench,
    /// nothing comes back from it, and the system already counts it from what was
    /// produced — so a ticket carrying it would count it twice and ask somebody to
    /// weigh bags that are counted in pieces.
    ///
    /// False is the right default: a category nobody has thought about should not be
    /// carried out of the store on a ticket by accident.
    /// </summary>
    public partial class MaterialCategoryIssuedOnTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IssuedOnTickets",
                table: "MaterialCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The seeder only fills an empty table, so a database that already has its
            // categories would never pick this up. Raw material is the one that goes
            // out on tickets; an administrator can adjust the rest in Master Data.
            migrationBuilder.Sql(
                """UPDATE "MaterialCategories" SET "IssuedOnTickets" = true WHERE "Name" = 'Raw Material';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuedOnTickets",
                table: "MaterialCategories");
        }
    }
}
