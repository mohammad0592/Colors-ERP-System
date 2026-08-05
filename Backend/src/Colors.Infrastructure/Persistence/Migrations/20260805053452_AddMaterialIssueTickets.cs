using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Colors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialIssueTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "issue_ticket_number_seq");

            migrationBuilder.AddColumn<int>(
                name: "IssueTicketId",
                table: "MaterialInventoryMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialIssueTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketNumber = table.Column<int>(type: "integer", nullable: false),
                    ShiftLineId = table.Column<int>(type: "integer", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialIssueTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialIssueTickets_ShiftLines_ShiftLineId",
                        column: x => x.ShiftLineId,
                        principalTable: "ShiftLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialIssueTickets_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialIssueTickets_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialIssueTicketLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    IssuedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialIssueTicketLines", x => x.Id);
                    table.CheckConstraint("ck_issue_lines_issued_positive", "\"IssuedQuantity\" > 0");
                    table.CheckConstraint("ck_issue_lines_returned_not_negative", "\"ReturnedQuantity\" >= 0");
                    table.CheckConstraint("ck_issue_lines_returned_within_issued", "\"ReturnedQuantity\" <= \"IssuedQuantity\"");
                    table.ForeignKey(
                        name: "FK_MaterialIssueTicketLines_MaterialIssueTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "MaterialIssueTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialIssueTicketLines_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssueTicketLines_MaterialId",
                table: "MaterialIssueTicketLines",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "ux_issue_lines_ticket_material",
                table: "MaterialIssueTicketLines",
                columns: new[] { "TicketId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssueTickets_ClosedByUserId",
                table: "MaterialIssueTickets",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssueTickets_IssuedByUserId",
                table: "MaterialIssueTickets",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_issue_tickets_line_status",
                table: "MaterialIssueTickets",
                columns: new[] { "ShiftLineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_issue_tickets_number",
                table: "MaterialIssueTickets",
                column: "TicketNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialIssueTicketLines");

            migrationBuilder.DropTable(
                name: "MaterialIssueTickets");

            migrationBuilder.DropColumn(
                name: "IssueTicketId",
                table: "MaterialInventoryMovements");

            migrationBuilder.DropSequence(
                name: "issue_ticket_number_seq");
        }
    }
}
