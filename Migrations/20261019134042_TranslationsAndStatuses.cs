using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityFix_backend.Migrations
{
    /// <inheritdoc />
    public partial class TranslationsAndStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Recipients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageEn",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToEn",
                table: "AppealUpdates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionTranslated",
                table: "Appeals",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Appeals",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "uk");

            migrationBuilder.AddColumn<string>(
                name: "TitleTranslated",
                table: "Appeals",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TranslationAttempts",
                table: "Appeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TranslationPending",
                table: "Appeals",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "MessageEn",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "AssignedToEn",
                table: "AppealUpdates");

            migrationBuilder.DropColumn(
                name: "DescriptionTranslated",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "TitleTranslated",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "TranslationAttempts",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "TranslationPending",
                table: "Appeals");
        }
    }
}
