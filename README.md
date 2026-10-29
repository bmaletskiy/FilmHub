# 🎬 FilmHub

**FilmHub** is an educational web application for browsing and managing a movie catalog. It is built with **ASP.NET Core MVC (.NET 10)** and **PostgreSQL**.

The application allows users to manage movies and genres, view movie details, write reviews, explore statistics, and interact with additional features such as a browser-based game.

## 🌐 Live Demo

**Live application:** [http://13.60.182.225](http://13.60.182.225)

> **Note:** The current demo uses HTTP and does not yet have user authentication. Movie and genre management operations are publicly accessible. Use test data only and do not submit sensitive information.

## ✨ Features

- **Movie management** — create, view, edit, and delete movies.
- **Genre management** — manage movie genres.
- **Movie details** — individual movie pages styled as Bootstrap landing pages.
- **Reviews** — submit and view movie reviews.
- **Statistics** — view application statistics.
- **Poster uploads** — upload movie posters using the File API. The Create form supports the current upload workflow.
- **Cloudinary integration** — store and serve movie posters through Cloudinary CDN.
- **Telegram notifications** — receive notifications through the Telegram Bot API.
- **Interactive game** — a JavaScript Canvas game featuring scoring, lives, pause, and restart functionality.
- **Google Analytics 4** — collect website usage analytics.

## 🛠 Technology Stack

| Area | Technology |
| --- | --- |
| Backend | C#, ASP.NET Core MVC (.NET 10) |
| Data access | Entity Framework Core |
| Database | PostgreSQL 16 |
| Frontend | Razor Views, HTML, CSS, JavaScript, Bootstrap |
| File uploads | ASP.NET Core File API |
| Image storage | Cloudinary |
| Notifications | Telegram Bot API |
| Analytics | Google Analytics 4 |
| Local environment | Docker, Docker Compose |
| Cloud hosting | AWS EC2 (Ubuntu) |
| CI/CD | GitHub Actions |
| Container registry | GitHub Container Registry (GHCR) |
| API testing | Postman |
| Automated testing | xUnit, browser-based E2E tests |

## 🚀 Local Setup (Windows)

### Requirements

- [Git](https://git-scm.com/downloads)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

Start Docker Desktop before running the commands below.

All commands are executed in **PowerShell**. Installing PostgreSQL or the .NET SDK separately is not required.

### 1. Clone the repository

```powershell
git clone https://github.com/bmaletskiy/FilmHub.git
cd FilmHub
```

### 2. Configure environment variables

Create a `.env` file in the project root, next to `docker-compose.yml`:

```dotenv
DB_PASSWORD=FilmHubLocal123

TMDB_TOKEN=

TELEGRAM_BOT_TOKEN=your_telegram_bot_token
TELEGRAM_CHAT_ID=your_telegram_chat_id

CLOUDINARY_CLOUD_NAME=your_cloudinary_cloud_name
CLOUDINARY_API_KEY=your_cloudinary_api_key
CLOUDINARY_API_SECRET=your_cloudinary_api_secret
```

- `DB_PASSWORD` — password for the local PostgreSQL database.
- `TMDB_TOKEN` — optional token for TMDB-related functionality.
- `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` — Telegram bot configuration.
- `CLOUDINARY_*` — credentials for Cloudinary image storage.

Telegram and Cloudinary credentials are required by the current Docker Compose configuration.

**Never commit your `.env` file or publish API keys and passwords.**

### 3. Start the application

```powershell
docker compose up -d --build
```

Docker will build the FilmHub image, download PostgreSQL 16, create the required containers and network, and start the services.

Check the container status:

```powershell
docker compose ps
```

### 4. Apply database migrations

This step is required when setting up the project for the first time.

Run the following commands from the project root:

```powershell
$dbContainer = docker compose ps -q db
$connection = "Host=localhost;Port=5432;Database=FilmHubDb;Username=postgres;Password=FilmHubLocal123"
```

Then execute:

```powershell
docker run --rm --network "container:$dbContainer" -v "${PWD}:/src" -w /src -e "ConnectionStrings__DefaultConnection=$connection" mcr.microsoft.com/dotnet/sdk:10.0 sh -c 'dotnet tool install --global dotnet-ef --version 10.* && export PATH="$PATH:/root/.dotnet/tools" && dotnet ef database update'
```

The command starts a temporary .NET SDK container and applies Entity Framework Core migrations to PostgreSQL.

If you changed `DB_PASSWORD`, update the password in the connection string as well.

### 5. Open the application

**Local URL:** [http://localhost:8081](http://localhost:8081)

On a fresh installation, the movie catalog may be empty. You can test the main functionality by:

1. Opening **Genres** and creating a genre.
2. Opening **Movies → Create** and adding a movie.
3. Uploading a poster if Cloudinary is configured.
4. Opening the movie's **Details** page.
5. Testing movie editing, deletion, and reviews.
6. Exploring the statistics page and the interactive game.

PostgreSQL data and uploaded files are stored in Docker volumes.

> **Important:** Do not run `docker compose down -v` unless you intentionally want to delete persistent Docker volumes and their data.

## 🧪 Testing

The project includes API and browser-based testing.

- **Postman API tests:** 35 tests passed, 0 failed, including validation and expected error responses such as HTTP 400 and 404.
- **End-to-end tests:** browser navigation and API create/read/delete scenarios were executed successfully.

To run the browser tests in headed mode:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/FilmHub.E2E.Tests/run-e2e.ps1 -Headed
```

## ☁️ Deployment and CI/CD

FilmHub is deployed on **AWS EC2 (Ubuntu)**.

The deployment workflow uses:

1. **GitHub Actions** to build the Docker image after changes are pushed to the `master` branch.
2. **GitHub Container Registry (GHCR)** to store published Docker images.
3. **AWS EC2** to host the production application.
4. A server-side update mechanism to periodically check for new images and update the application container.

The production environment uses Docker Compose and a separate production configuration.

## 📌 Project Status

FilmHub is an ongoing educational project developed as part of the **Web Technologies** course.

Authentication and authorization are planned as a separate development stage and are not currently implemented in the public demo.
