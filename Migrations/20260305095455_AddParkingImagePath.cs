using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSlot.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingImagePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ParkingSlots");

            migrationBuilder.AddColumn<string>(
                name: "ParkingImagePath",
                table: "ParkingSlots",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParkingImagePath",
                table: "ParkingSlots");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ParkingSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
