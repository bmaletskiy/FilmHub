# 🎬 FilmHub

**FilmHub** — навчальний вебзастосунок для перегляду та керування каталогом фільмів на **ASP.NET Core MVC (.NET 10)** із базою даних **PostgreSQL**.

У застосунку можна створювати фільми й жанри, переглядати інформацію про фільми, редагувати та видаляти записи. Сторінка кожного фільму оформлена як Bootstrap Landing Page.

## 🛠 Технології

| Частина проєкту | Технологія |
| --- | --- |
| Серверна частина | C#, ASP.NET Core MVC (.NET 10) |
| Доступ до даних | Entity Framework Core |
| База даних | PostgreSQL 16 |
| Інтерфейс | Razor Views, Bootstrap |
| Локальний запуск | Docker, Docker Compose |
| Хмарне розгортання | AWS EC2 |
| Автоматизація | GitHub Actions, GHCR |
| Аналітика | Google Analytics 4 |

## 🚀 Локальний запуск (Windows)

> **Потрібно:** [Git](https://git-scm.com/downloads) і [Docker Desktop](https://www.docker.com/products/docker-desktop/). Перед виконанням команд запустіть Docker Desktop. Окремо встановлювати PostgreSQL або .NET SDK не потрібно.
>
> Усі команди нижче виконуються у **PowerShell**.

### 1. Завантажте проєкт

```powershell
git clone https://github.com/bmaletskiy/FilmHub.git
cd FilmHub
```

### 2. Створіть файл `.env`

У корені проєкту (поряд із `docker-compose.yml`) створіть файл **`.env`** з таким вмістом:

```dotenv
DB_PASSWORD=FilmHubLocal123
TMDB_TOKEN=
```

`DB_PASSWORD` — пароль **лише для локальної тестової бази**. `TMDB_TOKEN` можна залишити порожнім: для звичайного створення та перегляду фільмів він не потрібен.

### 3. Запустіть контейнери

```powershell
docker compose up -d --build
```

Docker автоматично збере FilmHub, завантажить PostgreSQL 16, створить мережу й volumes та запустить контейнери.

Перевірка:

```powershell
docker compose ps
```

### 4. Застосуйте міграції бази даних

**Цей крок обов'язковий при першому запуску:** Docker створює базу `FilmHubDb`, але таблиці застосунку створюються міграціями EF Core.

У тій самій папці проєкту виконайте команди **послідовно**:

```powershell
$dbContainer = docker compose ps -q db
$connection = "Host=localhost;Port=5432;Database=FilmHubDb;Username=postgres;Password=FilmHubLocal123"
```

```powershell
docker run --rm --network "container:$dbContainer" -v "${PWD}:/src" -w /src -e "ConnectionStrings__DefaultConnection=$connection" mcr.microsoft.com/dotnet/sdk:10.0 sh -c 'dotnet tool install --global dotnet-ef --version 10.* && export PATH="$PATH:/root/.dotnet/tools" && dotnet ef database update'
```

> Команда запускає **тимчасовий контейнер .NET SDK** і застосовує міграції до PostgreSQL. Перший запуск може потребувати завантаження SDK та NuGet-пакетів.

### 5. Відкрийте сайт

**Адреса:** [http://localhost:8081](http://localhost:8081)

На новому комп'ютері каталог спочатку буде порожнім — це нормально. Для перевірки функцій:

1. Створіть жанр у розділі **Genres**.
2. Створіть фільм у **Movies → Create**, зазначте його жанр і, за бажанням, додайте постер.
3. Відкрийте **Details**, щоб переглянути Landing Page.
4. Перевірте **Edit** і **Delete**; відгуки — на сторінці фільму (відповідно до наявної версії застосунку).

## 🐳 Корисні команди Docker

| Дія | Команда |
| --- | --- |
| Перевірити стан контейнерів | `docker compose ps` |
| Переглянути логи | `docker compose logs -f` |
| Зупинити застосунок | `docker compose down` |
| Запустити знову | `docker compose up -d` |

Дані PostgreSQL та завантажені постери зберігаються в Docker volumes. **Не використовуйте `docker compose down -v`**, якщо хочете зберегти ці дані.

## ☁️ Розгортання

Production-версія FilmHub розміщується на **AWS EC2 (Ubuntu)**. GitHub Actions збирає Docker image після push у гілку `master` і публікує його в **GitHub Container Registry**. На сервері налаштовано періодичну перевірку оновлень і перевстановлення контейнера застосунку.

---

*Навчальний проєкт із дисципліни «Веб-технології».*
