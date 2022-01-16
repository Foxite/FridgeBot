using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FridgeBot.Migrations
{
    public partial class ServerInitializedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Entries_MessageId",
                table: "Entries");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InitializedAt",
                table: "Servers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_Entries_ServerId_FridgeMessageId",
                table: "Entries",
                columns: new[] { "ServerId", "FridgeMessageId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Entries_ServerId_FridgeMessageId",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "InitializedAt",
                table: "Servers");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_MessageId",
                table: "Entries",
                column: "MessageId");
        }
    }
}
