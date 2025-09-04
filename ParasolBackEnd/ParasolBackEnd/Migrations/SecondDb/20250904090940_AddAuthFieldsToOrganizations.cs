using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParasolBackEnd.Migrations.SecondDb
{
    /// <inheritdoc />
    public partial class AddAuthFieldsToOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Email",
                table: "organizations",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "organizations",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "KrsNumber",
                table: "organizations",
                newName: "krs_number");

            migrationBuilder.AlterColumn<string>(
                name: "organization_name",
                table: "organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "organizations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "krs_number",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_email",
                table: "organizations",
                column: "email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_organizations_email",
                table: "organizations");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "organizations",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "organizations",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "krs_number",
                table: "organizations",
                newName: "KrsNumber");

            migrationBuilder.AlterColumn<string>(
                name: "organization_name",
                table: "organizations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "organizations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "organizations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "KrsNumber",
                table: "organizations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
