using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSlot.Migrations
{
    /// <inheritdoc />
    public partial class AddExitVerificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExitMethod",
                table: "ParkingSlots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QrToken",
                table: "ParkingSlots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExitConfirmed",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExitConfirmedAt",
                table: "Bookings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExitScanAlertSent",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PenaltyApplied",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExitMethod",
                table: "ParkingSlots");

            migrationBuilder.DropColumn(
                name: "QrToken",
                table: "ParkingSlots");

            migrationBuilder.DropColumn(
                name: "ExitConfirmed",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ExitConfirmedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ExitScanAlertSent",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PenaltyApplied",
                table: "Bookings");
        }
    }
}
