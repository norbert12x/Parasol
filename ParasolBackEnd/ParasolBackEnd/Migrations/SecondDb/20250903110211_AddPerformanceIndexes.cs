using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParasolBackEnd.Migrations.SecondDb
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organizations_email",
                table: "organizations");

            migrationBuilder.RenameIndex(
                name: "IX_tags_category_id",
                table: "tags",
                newName: "ix_tags_category_id");

            migrationBuilder.RenameIndex(
                name: "IX_posts_organization_id",
                table: "posts",
                newName: "ix_posts_organization_id");

            migrationBuilder.RenameIndex(
                name: "IX_post_tags_tag_id",
                table: "post_tags",
                newName: "ix_post_tags_tag_id");

            migrationBuilder.RenameIndex(
                name: "IX_post_categories_category_id",
                table: "post_categories",
                newName: "ix_post_categories_category_id");

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
                name: "name",
                table: "tags",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "posts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "posts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "contact_phone",
                table: "posts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "contact_email",
                table: "posts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "organization_name",
                table: "organizations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
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

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "categories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "ix_tags_name",
                table: "tags",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_posts_created_at",
                table: "posts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_posts_status",
                table: "posts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_posts_title",
                table: "posts",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ix_post_tags_post_id",
                table: "post_tags",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_categories_post_id",
                table: "post_categories",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_name",
                table: "organizations",
                column: "organization_name");

            migrationBuilder.CreateIndex(
                name: "ix_categories_name",
                table: "categories",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tags_name",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ix_posts_created_at",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "ix_posts_status",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "ix_posts_title",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "ix_post_tags_post_id",
                table: "post_tags");

            migrationBuilder.DropIndex(
                name: "ix_post_categories_post_id",
                table: "post_categories");

            migrationBuilder.DropIndex(
                name: "ix_organizations_name",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "ix_categories_name",
                table: "categories");

            migrationBuilder.RenameIndex(
                name: "ix_tags_category_id",
                table: "tags",
                newName: "IX_tags_category_id");

            migrationBuilder.RenameIndex(
                name: "ix_posts_organization_id",
                table: "posts",
                newName: "IX_posts_organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_post_tags_tag_id",
                table: "post_tags",
                newName: "IX_post_tags_tag_id");

            migrationBuilder.RenameIndex(
                name: "ix_post_categories_category_id",
                table: "post_categories",
                newName: "IX_post_categories_category_id");

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
                name: "name",
                table: "tags",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "posts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "posts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "contact_phone",
                table: "posts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "contact_email",
                table: "posts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "organization_name",
                table: "organizations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "email",
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

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_email",
                table: "organizations",
                column: "email",
                unique: true);
        }
    }
}
