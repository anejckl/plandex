# Plandex

A Trello-style Kanban app with shared boards, card assignees, and built-in time tracking per card. Multiple users can collaborate on the same board in real time.

**Stack:** Angular 21 (standalone) + ASP.NET Core 10 + PostgreSQL 16

![CI](https://github.com/anejckl/plandex/actions/workflows/ci.yml/badge.svg)

---

## Try it live

A public instance runs at **https://plandex.anej.dev** so you can explore the collaboration flow without setting anything up locally.

Sign in with either of the pre-seeded demo accounts:

- `demo@plandex.dev` / `demo1234` — owns all three demo boards
- `demo2@plandex.dev` / `demo1234` — shared on the **Q2 Roadmap** board as a Member

> ⚠️ **It's a hobby instance on a small VPS.** Data may be reset without warning, and there's a **dev-stage limit of 50 active cards per user** to keep the demo usable. Archive cards to free up slots. Don't put anything you care about here.

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

On a fresh database (no users), the API seeds two demo accounts and three
populated boards so you can explore — including the collaboration flow —
without creating anything by hand:

- **Owner:** `demo@plandex.dev` / `demo1234` — owns all three seeded boards
- **Collaborator:** `demo2@plandex.dev` / `demo1234` — pre-shared on the
  **Q2 Roadmap** board as a Member

Log in as either user to see shared-board behavior end to end: changes made
by one show up live in the other's browser.

Seeding runs only once, and only when the `users` table is empty — it will
never touch an existing database. To re-seed, drop the `pgdata` volume:

```bash
docker compose down -v
docker compose up --build
```

---

## Collaboration

Boards are multi-user. Each board has **members** in one of two roles:

- **Owner** — whoever created the board. Can rename the board, delete it,
  add/remove members, and permanently purge archived cards. There must
  always be at least one owner — the last owner cannot leave.
- **Member** — everyone else. Can read the board, create and edit lists,
  cards, labels and checklists, track time, and assign themselves or
  other members to cards. Cannot rename the board, delete it, manage
  members, or hard-delete archived cards.

### Adding a member

Click the member avatar stack in the board header to open the **Members**
modal. As the owner, type the email of an existing registered user and
click Add. There's no invite email — the target must already have a
plandex account. Unknown emails and duplicates surface inline errors in
the modal.

### Card assignees

Open a card and click **+ Assign** in the Assignees section to pick from
the board's current members. Multiple people can be assigned to the same
card; assignees render as stacked avatars on the card tile. Removing a
member from a board automatically drops any card assignments they held
on that board.

### Real-time updates

Any change made by one member — creating or editing a card, moving it
between lists, renaming the board, assigning someone, adding or removing
a member — is pushed to every other connected viewer over Server-Sent
Events. No refresh needed. The access token is auto-refreshed when the
EventSource connection expires, so long-lived sessions stay connected.

### Card limit (dev stage only)

While plandex is in dev stage there's a hardcoded cap of **50 active cards
per user** (enforced in `CardService.MaxActiveCardsPerUser`). Archived
cards don't count — archiving a card frees up a slot. The 51st `POST` to
`/api/lists/{id}/cards` returns HTTP 429 with a clear error message, and
the frontend surfaces it as an alert. Remove or raise the constant when
the project leaves dev stage.

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
