using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityFix_backend.Migrations
{
    /// <inheritdoc />
    public partial class DeadlinesRatingsSupportTelegram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyTelegram",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramLinkCode",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TelegramLinkExpires",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TelegramState",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "AppealUpdates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNotifiedAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RatedAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Appeals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RatingComment",
                table: "Appeals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReopenCount",
                table: "Appeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SupportCount",
                table: "Appeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "AppealFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppealSupports",
                columns: table => new
                {
                    AppealId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealSupports", x => new { x.AppealId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AppealSupports_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealSupports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramLinkCode",
                table: "Users",
                column: "TelegramLinkCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_DueAt",
                table: "Appeals",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppealSupports_UserId",
                table: "AppealSupports",
                column: "UserId");

            // Existing appeals get the default deadlines (Low 14, Normal 7, Urgent 2 days from creation).
            migrationBuilder.Sql("""
                UPDATE "Appeals" SET "DueAt" = "CreatedAt" + CASE "Priority" WHEN 0 THEN interval '14 days' WHEN 2 THEN interval '2 days' ELSE interval '7 days' END;
                """);
            // Closed appeals: when they reached their current status, or their last change.
            migrationBuilder.Sql("""
                UPDATE "Appeals" a SET "ClosedAt" = COALESCE(
                    (SELECT max(u."ChangedAt") FROM "AppealUpdates" u WHERE u."AppealId" = a."Id" AND u."NewStatus" = a."Status" AND u."OldStatus" <> a."Status"),
                    a."UpdatedAt")
                WHERE a."Status" IN (2, 3, 7);
                """);
            // Appeals already past their deadline show as overdue but do not flood the heads with notifications on deploy.
            migrationBuilder.Sql("""
                UPDATE "Appeals" SET "OverdueNotifiedAt" = now()
                WHERE "DueAt" < now() AND "Status" NOT IN (2, 3, 7);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealSupports");

            migrationBuilder.DropIndex(
                name: "IX_Users_TelegramLinkCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Appeals_DueAt",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "NotifyTelegram",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkExpires",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramState",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "AppealUpdates");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAt",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "RatedAt",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "RatingComment",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "ReopenCount",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "SupportCount",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "AppealFiles");
        }
    }
}
