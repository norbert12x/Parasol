using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParasolBackEnd.Migrations.SecondDb
{
    /// <inheritdoc />
    public partial class AddContactEmailAndPhoneToPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "posts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "posts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "posts");
        }
    }
}
