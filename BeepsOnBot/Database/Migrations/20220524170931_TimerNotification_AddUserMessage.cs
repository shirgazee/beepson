using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeepsOnBot.Database.Migrations
{
    public partial class TimerNotification_AddUserMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserMessage",
                table: "TimerNotifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserMessage",
                table: "TimerNotifications");
        }
    }
}
