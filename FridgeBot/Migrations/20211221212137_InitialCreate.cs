using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FridgeBot.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Emotes",
                columns: table => new
                {
                    ServerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmoteString = table.Column<string>(type: "text", nullable: false),
                    MinimumToAdd = table.Column<int>(type: "integer", nullable: false),
                    MaximumToRemove = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emotes", x => new { x.ServerId, x.EmoteString });
                    table.ForeignKey(
                        name: "FK_Emotes_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    FridgeMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entries_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FridgeEntryEmote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FridgeEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmoteString = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FridgeEntryEmote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FridgeEntryEmote_Entries_FridgeEntryId",
                        column: x => x.FridgeEntryId,
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_MessageId",
                table: "Entries",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_ServerId_ChannelId_MessageId",
                table: "Entries",
                columns: new[] { "ServerId", "ChannelId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FridgeEntryEmote_FridgeEntryId_EmoteString",
                table: "FridgeEntryEmote",
                columns: new[] { "FridgeEntryId", "EmoteString" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Emotes");

            migrationBuilder.DropTable(
                name: "FridgeEntryEmote");

            migrationBuilder.DropTable(
                name: "Entries");

            migrationBuilder.DropTable(
                name: "Servers");
        }
    }
}
