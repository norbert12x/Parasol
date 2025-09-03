using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParasolBackEnd.Migrations.SecondDb
{
    /// <inheritdoc />
    public partial class RemoveContactInfoAndOfferDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_info",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "offer_description",
                table: "posts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_info",
                table: "posts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "offer_description",
                table: "posts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
