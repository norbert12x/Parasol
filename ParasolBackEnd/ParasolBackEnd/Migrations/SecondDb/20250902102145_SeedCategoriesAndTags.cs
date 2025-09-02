using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParasolBackEnd.Migrations.SecondDb
{
    /// <inheritdoc />
    public partial class SeedCategoriesAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dodaj kategorie
            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Edukacja" },
                    { 2, "Zdrowie" },
                    { 3, "Ekologia" },
                    { 4, "Kultura" },
                    { 5, "Sport" },
                    { 6, "Pomoc społeczna" },
                    { 7, "Nauka i technologia" },
                    { 8, "Młodzież" }
                });

            // Dodaj tagi dla kategorii Edukacja
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 1, "Robotyka", 1 },
                    { 2, "Programowanie", 1 },
                    { 3, "Matematyka", 1 },
                    { 4, "Języki obce", 1 },
                    { 5, "Historia", 1 },
                    { 6, "Geografia", 1 },
                    { 7, "Fizyka", 1 },
                    { 8, "Chemia", 1 }
                });

            // Dodaj tagi dla kategorii Zdrowie
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 9, "Opieka medyczna", 2 },
                    { 10, "Psychologia", 2 },
                    { 11, "Rehabilitacja", 2 },
                    { 12, "Zdrowie psychiczne", 2 },
                    { 13, "Dietetyka", 2 },
                    { 14, "Fizjoterapia", 2 },
                    { 15, "Stomatologia", 2 },
                    { 16, "Oftalmologia", 2 }
                });

            // Dodaj tagi dla kategorii Ekologia
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 17, "Ochrona zwierząt", 3 },
                    { 18, "Recykling", 3 },
                    { 19, "Energia odnawialna", 3 },
                    { 20, "Ochrona środowiska", 3 },
                    { 21, "Zrównoważony rozwój", 3 },
                    { 22, "Ogródnictwo", 3 },
                    { 23, "Las", 3 },
                    { 24, "Woda", 3 }
                });

            // Dodaj tagi dla kategorii Kultura
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 25, "Sztuka", 4 },
                    { 26, "Teatr", 4 },
                    { 27, "Muzyka", 4 },
                    { 28, "Film", 4 },
                    { 29, "Literatura", 4 },
                    { 30, "Fotografia", 4 },
                    { 31, "Taniec", 4 },
                    { 32, "Rękodzieło", 4 }
                });

            // Dodaj tagi dla kategorii Sport
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 33, "Piłka nożna", 5 },
                    { 34, "Koszykówka", 5 },
                    { 35, "Pływanie", 5 },
                    { 36, "Tenis", 5 },
                    { 37, "Bieganie", 5 },
                    { 38, "Rower", 5 },
                    { 39, "Siłownia", 5 },
                    { 40, "Joga", 5 }
                });

            // Dodaj tagi dla kategorii Pomoc społeczna
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 41, "Pomoc ubogim", 6 },
                    { 42, "Bezdomność", 6 },
                    { 43, "Seniorzy", 6 },
                    { 44, "Dzieci", 6 },
                    { 45, "Uchodźcy", 6 },
                    { 46, "Niepełnosprawni", 6 },
                    { 47, "Przemoc domowa", 6 },
                    { 48, "Bezrobocie", 6 }
                });

            // Dodaj tagi dla kategorii Nauka i technologia
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 49, "Sztuczna inteligencja", 7 },
                    { 50, "Informatyka", 7 },
                    { 51, "Biotechnologia", 7 },
                    { 52, "Nanotechnologia", 7 },
                    { 53, "Kosmos", 7 },
                    { 54, "Medycyna", 7 },
                    { 55, "Inżynieria", 7 },
                    { 56, "Badania naukowe", 7 }
                });

            // Dodaj tagi dla kategorii Młodzież
            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "name", "category_id" },
                values: new object[,]
                {
                    { 57, "Wolontariat", 8 },
                    { 58, "Liderstwo", 8 },
                    { 59, "Aktywizm", 8 },
                    { 60, "Edukacja obywatelska", 8 },
                    { 61, "Międzykulturowość", 8 },
                    { 62, "Przedsiębiorczość", 8 },
                    { 63, "Kreatywność", 8 },
                    { 64, "Komunikacja", 8 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Usuń tagi
            migrationBuilder.DeleteData(
                table: "tags",
                keyColumn: "id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64 });

            // Usuń kategorie
            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        }
    }
}
