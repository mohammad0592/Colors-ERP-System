using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductArabicName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArabicName",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // The factory's eight products as the seeder made them, given their Arabic names
            // where they still have none (section 19.33). Matched on the English name, so a
            // product renamed or added since is left for Master Data — and a name already
            // typed in is never overwritten.
            migrationBuilder.Sql(
                """
                UPDATE "Products" p
                SET "ArabicName" = n.arabic
                FROM (VALUES
                    ('Normal Big Plate', 'طبق كبير عادي'),
                    ('Normal Small Plate', 'طبق صغير عادي'),
                    ('Absorbent Big Plate', 'طبق امتصاص كبير'),
                    ('Absorbent Small Plate', 'طبق امتصاص صغير'),
                    ('1-Compartment Lunch Box', 'لانش بوكس غير مقطع'),
                    ('3-Compartment Lunch Box', 'لانش بوكس مقطع'),
                    ('Large Burger Box', 'علبة برغر كبيرة'),
                    ('Small Burger Box', 'علبة برغر صغيرة')
                ) AS n(english, arabic)
                WHERE p."Name" = n.english AND p."ArabicName" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArabicName",
                table: "Products");
        }
    }
}
