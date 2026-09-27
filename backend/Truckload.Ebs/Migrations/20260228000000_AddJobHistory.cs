using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truckload.Ebs.Migrations
{
    /// <inheritdoc />
    public partial class AddJobHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<string>(type: "text", nullable: false),
                    Cargo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Destination = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Distance = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobHistoryEntries_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobHistoryEntries_ChannelId_CompletedAt",
                table: "JobHistoryEntries",
                columns: new[] { "ChannelId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobHistoryEntries");
        }
    }
}
