# Plandex

A Trello-style Kanban project management app with built-in time tracking per card.

**Stack:** Angular 21 (standalone) + ASP.NET Core 10 + PostgreSQL 16

![CI](https://github.com/anejckl/plandex/actions/workflows/ci.yml/badge.svg)

---

## Prerequisites

- .NET 10 SDK
- `dotnet tool install --global dotnet-ef`
- Node 20+ / npm
- `npm i -g @angular/cli@21`
- Docker Desktop

---

## Dev loop (recommended)

Only Postgres runs in Docker; API and frontend run on the host for fast reload.

```bash
# 1. Start Postgres
docker compose -f docker-compose.dev.yml up -d

# 2. Start API (auto-applies migrations on boot)
cd api
dotnet run

# 3. Start frontend
cd frontend
npm install
ng serve
```

Open http://localhost:4200

Dev secrets for the API (connection string + JWT secret) live in
`api/appsettings.Development.json` and are **for local dev only** — never use
those values in production.

---

## Full stack (prod-like, all containers)

First, create a `.env` file at the repo root with real secrets:

```bash
cp .env.example .env
# edit .env and set POSTGRES_PASSWORD and JWT_SECRET
```

Then build and run:

```bash
docker compose up --build
```

Open http://localhost:4200

`docker compose` will refuse to start if `POSTGRES_PASSWORD` or `JWT_SECRET`
are missing.

### Demo data

On a fresh database (no users), the API automatically seeds a demo account
with three populated boards so you can explore without creating everything
by hand:

- **Email:** `demo@plandex.dev`
- **Password:** `demo1234`

Seeding runs only once, and only when the `users` table is empty — it will
never touch an existing database. To re-seed, drop the `pgdata` volume:

```bash
docker compose down -v
docker compose up --build
```

---

## Tests

```bash
# Backend
dotnet test plandex.sln

# Frontend
cd frontend
npm test
```

---

## Database management

> Always use `docker compose down` (never `down -v`) to preserve the `pgdata` volume.

Backup:

```bash
docker exec plandex-db-1 pg_dump -U plandex plandex > backup_$(date +%Y%m%d).sql
```

Creating a new EF migration after model changes:

```bash
cd api
dotnet ef migrations add <Name>
```

Migrations are applied automatically on API startup.

---

## License

[MIT](LICENSE)
