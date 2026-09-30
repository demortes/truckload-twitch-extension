using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truckload.Ebs.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ChannelId",
                table: "TelemetrySnapshots",
                type: "character varying(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ChannelId",
                table: "JobHistoryEntries",
                type: "character varying(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ChannelId",
                table: "TelemetrySnapshots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)");

            migrationBuilder.AlterColumn<string>(
                name: "ChannelId",
                table: "JobHistoryEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)");
        }
    }
}
