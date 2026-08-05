using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The factory received three new templates — two meal boxes and a 3-compartment
    /// clamshell — so a product stops being describable as type × size × absorbent.
    /// `Moulds` and `Products` replace that grid, and `ShiftLines` gains the mould
    /// mounted for the shift.
    ///
    /// `PlateSizes` is dropped rather than migrated. Big and Small now live inside the
    /// product's name, and nothing yet references the table — the bags and pallets
    /// that would have pointed at it arrive in phases 9 and 10. So there is no data to
    /// carry across, only two rows whose meaning moved.
    ///
    /// Going back recreates the table empty. Its two rows would have to be typed again,
    /// which is a second of work and cannot silently lose production history.
    /// </summary>
    public partial class MouldsAndProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlateSizes");

            migrationBuilder.AddColumn<int>(
                name: "MouldId",
                table: "ShiftLines",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Moulds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moulds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MouldId = table.Column<int>(type: "integer", nullable: false),
                    ProductTypeId = table.Column<int>(type: "integer", nullable: false),
                    IsAbsorbent = table.Column<bool>(type: "boolean", nullable: false),
                    PiecesPerBag = table.Column<int>(type: "integer", nullable: false),
                    SmallBagsPerBag = table.Column<int>(type: "integer", nullable: false),
                    BagsPerPallet = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Moulds_MouldId",
                        column: x => x.MouldId,
                        principalTable: "Moulds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftLines_MouldId",
                table: "ShiftLines",
                column: "MouldId");

            migrationBuilder.CreateIndex(
                name: "ux_moulds_name",
                table: "Moulds",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductTypeId",
                table: "Products",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "ux_products_mould_absorbent",
                table: "Products",
                columns: new[] { "MouldId", "IsAbsorbent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_products_name",
                table: "Products",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftLines_Moulds_MouldId",
                table: "ShiftLines",
                column: "MouldId",
                principalTable: "Moulds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftLines_Moulds_MouldId",
                table: "ShiftLines");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Moulds");

            migrationBuilder.DropIndex(
                name: "IX_ShiftLines_MouldId",
                table: "ShiftLines");

            migrationBuilder.DropColumn(
                name: "MouldId",
                table: "ShiftLines");

            migrationBuilder.CreateTable(
                name: "PlateSizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlateSizes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_plate_sizes_name",
                table: "PlateSizes",
                column: "Name",
                unique: true);
        }
    }
}
