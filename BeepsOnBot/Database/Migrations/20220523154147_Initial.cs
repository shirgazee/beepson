using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeepsOnBot.Database.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatPreferences",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", nullable: false),
                    LastMessages = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatPreferences", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "TimerNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotifyAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimerNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimerNotifications_ChatId_NotifyAt",
                table: "TimerNotifications",
                columns: new[] { "ChatId", "NotifyAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatPreferences");

            migrationBuilder.DropTable(
                name: "TimerNotifications");
        }
    }
}
