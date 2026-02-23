using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSlot.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneAndParkingScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "AvailableTo",
                table: "ParkingSlots",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AvailableFrom",
                table: "ParkingSlots",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "OwnerPhone",
                table: "ParkingSlots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ParkingBadge",
                table: "ParkingSlots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkingImageBase64",
                table: "ParkingSlots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingScore",
                table: "ParkingSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ParkingScoreDetails",
                table: "ParkingSlots",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "BookingTo",
                table: "Bookings",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "BookingFrom",
                table: "Bookings",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerPhone",
                table: "ParkingSlots");

            migrationBuilder.DropColumn(
                name: "ParkingBadge",
                table: "ParkingSlots");

            migrationBuilder.DropColumn(
                name: "ParkingImageBase64",
                table: "ParkingSlots");

            migrationBuilder.DropColumn(
                name: "ParkingScore",
                table: "ParkingSlots");

            migrationBuilder.DropColumn(
                name: "ParkingScoreDetails",
                table: "ParkingSlots");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AvailableTo",
                table: "ParkingSlots",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AvailableFrom",
                table: "ParkingSlots",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "BookingTo",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "BookingFrom",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");
        }
    }
}
