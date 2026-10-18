using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilmHub.Migrations
{
    /// <inheritdoc />
    public partial class MovieGenresManyToMany : Migration
    {
        /// <inheritdoc />

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Створюємо таблицю зв'язків фільмів і жанрів
            migrationBuilder.CreateTable(
                name: "MovieGenres",
                columns: table => new
                {
                    GenresId = table.Column<int>(type: "integer", nullable: false),
                    MoviesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_MovieGenres",
                        x => new { x.GenresId, x.MoviesId });

                    table.ForeignKey(
                        name: "FK_MovieGenres_Genres_GenresId",
                        column: x => x.GenresId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_MovieGenres_Movies_MoviesId",
                        column: x => x.MoviesId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovieGenres_MoviesId",
                table: "MovieGenres",
                column: "MoviesId");

            // 2. Переносимо старі жанри до нової таблиці
            migrationBuilder.Sql("""
        INSERT INTO "MovieGenres" ("GenresId", "MoviesId")
        SELECT "GenreId", "Id"
        FROM "Movies";
        """);

            // 3. Видаляємо старий зовнішній ключ та індекс
            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Genres_GenreId",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Movies_GenreId",
                table: "Movies");

            // 4. Видаляємо старе поле тільки після перенесення
            migrationBuilder.DropColumn(
                name: "GenreId",
                table: "Movies");
        }

        /// <inheritdoc />

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Повертаємо колонку для одного жанру
            migrationBuilder.AddColumn<int>(
                name: "GenreId",
                table: "Movies",
                type: "integer",
                nullable: true);

            // Для кожного фільму вибираємо один із його жанрів
            migrationBuilder.Sql("""
        UPDATE "Movies" AS m
        SET "GenreId" = (
            SELECT MIN(mg."GenresId")
            FROM "MovieGenres" AS mg
            WHERE mg."MoviesId" = m."Id"
        );
        """);

            // Старий GenreId обов'язковий, тому перевіряємо дані
            migrationBuilder.Sql("""
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM "Movies" WHERE "GenreId" IS NULL)
            THEN
                RAISE EXCEPTION
                    'Cannot revert: some movies have no genres.';
            END IF;
        END $$;
        """);

            migrationBuilder.AlterColumn<int>(
                name: "GenreId",
                table: "Movies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_GenreId",
                table: "Movies",
                column: "GenreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Genres_GenreId",
                table: "Movies",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                name: "MovieGenres");
        }

    }
}
