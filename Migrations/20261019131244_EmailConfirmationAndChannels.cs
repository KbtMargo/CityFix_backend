using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityFix_backend.Migrations
{
    /// <inheritdoc />
    public partial class EmailConfirmationAndChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "uk");

            migrationBuilder.AddColumn<bool>(
                name: "NotifyEmail",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyInApp",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifySms",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmailState",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "InApp",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SmsState",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EmailState_SmsState",
                table: "Notifications",
                columns: new[] { "EmailState", "SmsState" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_EmailState_SmsState",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyInApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifySms",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EmailState",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "InApp",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SmsState",
                table: "Notifications");
        }
    }
}
