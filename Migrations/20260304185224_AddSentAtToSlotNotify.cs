using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSlot.Migrations
{
    /// <inheritdoc />
    public partial class AddSentAtToSlotNotify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "SlotNotifyRequests",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "SlotNotifyRequests");
        }
    }
}
