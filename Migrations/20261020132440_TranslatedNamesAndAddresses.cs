using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityFix_backend.Migrations
{
    /// <inheritdoc />
    public partial class TranslatedNamesAndAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullNameTranslated",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressTranslated",
                table: "Appeals",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressEn",
                table: "Acabs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Acabs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullNameTranslated",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AddressTranslated",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "AddressEn",
                table: "Acabs");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Acabs");
        }
    }
}
