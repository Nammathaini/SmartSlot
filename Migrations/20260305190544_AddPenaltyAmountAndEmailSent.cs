using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSlot.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltyAmountAndEmailSent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyAmount",
                table: "Bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "PenaltyEmailSent",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PenaltyAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PenaltyEmailSent",
                table: "Bookings");
        }
    }
}
