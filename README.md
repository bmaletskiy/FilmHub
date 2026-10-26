FilmHub — інструкція локального запуску

FilmHub — вебзастосунок для керування каталогом фільмів, розроблений на ASP.NET Core MVC (.NET 10) із використанням PostgreSQL, Entity Framework Core та Bootstrap.

1. Необхідне програмне забезпечення

Перед початком встановіть:

Git

Docker Desktop

Запустіть Docker Desktop і переконайтеся, що Docker Engine працює.

Окремо встановлювати PostgreSQL або .NET SDK не потрібно.

2. Завантаження проєкту

Відкрийте PowerShell і виконайте:

git clone https://github.com/bmaletskiy/FilmHub.git
cd FilmHub

3. Налаштування середовища

У корені проєкту створіть файл .env із таким вмістом:

DB_PASSWORD=FilmHubLocal123
TMDB_TOKEN=

DB_PASSWORD — локальний пароль PostgreSQL.

TMDB_TOKEN можна залишити порожнім. Він потрібний лише для імпорту фільмів через TMDB API.

4. Запуск контейнерів

У PowerShell із папки FilmHub виконайте:

docker compose up -d --build

Команда збере ASP.NET Core застосунок і запустить два основні контейнери:

app — FilmHub;

db — PostgreSQL 16.

Перевірте їхній стан:

docker compose ps

5. Створення таблиць бази даних

Під час першого запуску необхідно застосувати міграції Entity Framework Core.

Для цього виконайте наведені команди в PowerShell, перебуваючи в корені проєкту.

Спочатку отримайте ідентифікатор контейнера PostgreSQL:

$dbContainer = docker compose ps -q db

Задайте підключення до локальної бази:

$connection = "Host=localhost;Port=5432;Database=FilmHubDb;Username=postgres;Password=FilmHubLocal123"

Тепер застосуйте міграції через тимчасовий контейнер .NET SDK:

docker run --rm --network "container:$dbContainer" -v "${PWD}:/src" -w /src -e "ConnectionStrings__DefaultConnection=$connection" mcr.microsoft.com/dotnet/sdk:10.0 sh -c 'dotnet tool install --global dotnet-ef --version 10.* && export PATH="$PATH:/root/.dotnet/tools" && dotnet ef database update'

Ця команда створить необхідні таблиці відповідно до міграцій проєкту.

6. Відкриття FilmHub

Після застосування міграцій відкрийте:

http://localhost:8081

База даних після першого запуску буде порожньою. Це нормально.

Для перевірки роботи застосунку можна:

Відкрити розділ Genres і створити жанр.

Перейти в Movies і створити фільм.

Заповнити назву, рік, опис, вибрати жанр і завантажити постер.

Відкрити Details для перегляду Landing Page.

Перевірити редагування, видалення та функції відгуків.

7. Керування застосунком

Перегляд запущених контейнерів:

docker compose ps

Зупинка:

docker compose down

Повторний запуск:

docker compose up -d

Дані PostgreSQL зберігаються в Docker volume і не видаляються після звичайної зупинки контейнерів.

Увага: команда docker compose down -v видаляє також volumes, тому її не слід використовувати, якщо потрібно зберегти дані.
