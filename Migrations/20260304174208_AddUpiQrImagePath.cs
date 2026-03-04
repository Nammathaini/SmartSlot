using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSlot.Migrations
{
    /// <inheritdoc />
    public partial class AddUpiQrImagePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UpiQrImagePath",
                table: "ParkingSlots",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpiQrImagePath",
                table: "ParkingSlots");
        }
    }
}
