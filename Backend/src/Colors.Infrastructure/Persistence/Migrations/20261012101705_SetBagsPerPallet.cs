using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SetBagsPerPallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The factory's own counts for a full pallet of each product. A pallet completes
            // itself at this number and refuses the next bag, so a wrong figure builds
            // short or overfull pallets. The boxes were seeded at 21, copied from the old
            // meal box, until the factory said (specification section 18, question 10).
            // By name, once: these are the eight products the factory listed.
            migrationBuilder.Sql("""
                UPDATE "Products" p SET "BagsPerPallet" = n.bags
                  FROM (VALUES ('Normal Big Plate', 15),
                               ('Normal Small Plate', 15),
                               ('Absorbent Big Plate', 15),
                               ('Absorbent Small Plate', 15),
                               ('1-Compartment Lunch Box', 23),
                               ('3-Compartment Lunch Box', 23),
                               ('Large Burger Box', 30),
                               ('Small Burger Box', 40))
                       AS n(name, bags)
                 WHERE p."Name" = n.name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to put back: the earlier figures were provisional, and a pallet
            // already built keeps the bags it was built with.
        }
    }
}
