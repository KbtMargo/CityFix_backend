using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace CityFix_backend.Migrations
{
    /// <inheritdoc />
    public partial class CodesResponsibleSpatial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_Categories_CategoryId",
                table: "Appeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_Recipients_RecipientId",
                table: "Appeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_Users_AuthorId",
                table: "Appeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Acabs_AcabId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Categories");


            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Recipients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ContactInfo",
                table: "Recipients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AssignedTo",
                table: "AppealUpdates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewPriority",
                table: "AppealUpdates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OldPriority",
                table: "AppealUpdates",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Lng",
                table: "Appeals",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Lat",
                table: "Appeals",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcabId",
                table: "Appeals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponsibleKind",
                table: "Appeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Appeals",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "HeadCode",
                table: "Acabs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResidentCode",
                table: "Acabs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Acabs" SET "HeadCode" = upper(replace("InvitationCode", ' ', ''));
                UPDATE "Acabs" SET "ResidentCode" = (
                    SELECT string_agg(substr('ABCDEFGHJKLMNPQRSTUVWXYZ23456789', 1 + floor(random() * 32)::int, 1), '')
                    FROM generate_series(1, 10) WHERE "Acabs"."Id" IS NOT NULL);
                """);

            migrationBuilder.DropColumn(
                name: "InvitationCode",
                table: "Acabs");

            migrationBuilder.Sql("""
                UPDATE "Appeals" SET "ResponsibleKind" = 1;
                UPDATE "Appeals" a SET "AcabId" = u."AcabId" FROM "Users" u WHERE u."Id" = a."AuthorId";
                UPDATE "Users" SET "Email" = lower(trim("Email"));
                """);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Appeals",
                type: "geography (point, 4326)",
                nullable: true,
                computedColumnSql: "ST_SetSRID(ST_MakePoint(\"Lng\"::double precision, \"Lat\"::double precision), 4326)::geography",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_AcabId_Status",
                table: "Appeals",
                columns: new[] { "AcabId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_CreatedAt",
                table: "Appeals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_Location",
                table: "Appeals",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Acabs_HeadCode",
                table: "Acabs",
                column: "HeadCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acabs_ResidentCode",
                table: "Acabs",
                column: "ResidentCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_Acabs_AcabId",
                table: "Appeals",
                column: "AcabId",
                principalTable: "Acabs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_Categories_CategoryId",
                table: "Appeals",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_Recipients_RecipientId",
                table: "Appeals",
                column: "RecipientId",
                principalTable: "Recipients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_Users_AuthorId",
                table: "Appeals",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Acabs_AcabId",
                table: "Users",
                column: "AcabId",
                principalTable: "Acabs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_Acabs_AcabId",
                table: "Appeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_Categories_CategoryId",
                table: "Appeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_Recipients_RecipientId",
                table: "Appeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_Users_AuthorId",
                table: "Appeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Acabs_AcabId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Appeals_AcabId_Status",
                table: "Appeals");

            migrationBuilder.DropIndex(
                name: "IX_Appeals_CreatedAt",
                table: "Appeals");

            migrationBuilder.DropIndex(
                name: "IX_Appeals_Location",
                table: "Appeals");

            migrationBuilder.DropIndex(
                name: "IX_Acabs_HeadCode",
                table: "Acabs");

            migrationBuilder.DropIndex(
                name: "IX_Acabs_ResidentCode",
                table: "Acabs");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "AppealUpdates");

            migrationBuilder.DropColumn(
                name: "NewPriority",
                table: "AppealUpdates");

            migrationBuilder.DropColumn(
                name: "OldPriority",
                table: "AppealUpdates");

            migrationBuilder.DropColumn(
                name: "AcabId",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "ResponsibleKind",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "HeadCode",
                table: "Acabs");

            migrationBuilder.DropColumn(
                name: "ResidentCode",
                table: "Acabs");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Recipients",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ContactInfo",
                table: "Recipients",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Categories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Categories",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Lng",
                table: "Appeals",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Lat",
                table: "Appeals",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationCode",
                table: "Acabs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_Categories_CategoryId",
                table: "Appeals",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_Recipients_RecipientId",
                table: "Appeals",
                column: "RecipientId",
                principalTable: "Recipients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_Users_AuthorId",
                table: "Appeals",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Acabs_AcabId",
                table: "Users",
                column: "AcabId",
                principalTable: "Acabs",
                principalColumn: "Id");
        }
    }
}
