# League Classic Archive

A classic-style guides, forum, and resource site for **League Classic** (launching July 29, 2026).
Old-school MOBAFire style.

## Stack

| Layer    | Choice |
|----------|--------|
| Runtime  | .NET 10 (ASP.NET Core, Razor Pages) |
| Frontend | Server-rendered Razor + [htmx](https://htmx.org) (no SPA build step) |
| Data     | PostgreSQL 18 via EF Core (Npgsql) |
| Auth     | ASP.NET Core Identity (built-in login/register UI) |
| Markdown | Markdig (raw HTML disabled for safety) |
| Caching  | ASP.NET Core Output Caching (put Cloudflare in front for launch) |

## Prerequisites

- **.NET 10 SDK** — installed at `~/.dotnet` on this machine. If `dotnet` isn't on your PATH,
  add this to your `~/.bashrc`:
  ```bash
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
  ```
- **Docker** (for the local Postgres) — or a native Postgres if you prefer.
- **EF Core CLI**: `dotnet tool install --global dotnet-ef` (already installed here).

## Run it

```bash
# 1. Start Postgres (host port 5433 to avoid clashing with a native 5432)
docker compose up -d db

# 2. Run the web app (applies migrations + seeds starter boards on first start)
dotnet run --project src/LeagueClassic.Web

# 3. Open it
#    App:     http://localhost:5160
#    Adminer: http://localhost:8080   (system: PostgreSQL, server: db, user/pass: leagueclassic / leagueclassic_dev)
```

Register an account at `/Identity/Account/Register` (email confirmation is off in Development).

## Troubleshooting a new machine

If `docker compose up -d db` or `dotnet run` fail right after setting up on a fresh machine, it's
almost always one of these three gaps, not a code problem:

- **`permission denied ... docker.sock`** — your user isn't in the `docker` group yet:
  ```bash
  sudo usermod -aG docker $USER
  newgrp docker   # or just log out and back in
  ```
- **`docker: unknown command: docker compose`** — only the base `docker.io` package is
  installed; the Compose v2 plugin isn't:
  ```bash
  sudo apt-get update && sudo apt-get install -y docker-compose-v2
  ```
- **`dotnet: command not found`** even though `DOTNET_ROOT`/`PATH` are set per above — the SDK
  itself was never installed at `~/.dotnet`:
  ```bash
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0
  ```
  This installs to `~/.dotnet` by default, matching the PATH setup above. Verify with
  `dotnet --version`.

## Build

```bash
dotnet build                       # whole solution
dotnet build src/LeagueClassic.Web # just the web project
```

## Database migrations

```bash
# After changing anything in Data/ (entities or DbContext):
dotnet ef migrations add <Name> --project src/LeagueClassic.Web --output-dir Data/Migrations

# Apply manually (the app also auto-applies on startup):
dotnet ef database update --project src/LeagueClassic.Web

# CI guard — fails if the model drifted from the last migration:
dotnet ef migrations has-pending-model-changes --project src/LeagueClassic.Web
```

## Project layout

```
LeagueClassic.slnx
docker-compose.yml            # Postgres 18 + Adminer
src/LeagueClassic.Web/
  Program.cs                  # DI wiring: EF, Identity, output cache, markdown
  Data/
    ApplicationUser.cs        # Identity user (+ profile fields)
    Entities.cs               # Category > Board > ForumThread > Post; Guide, Comment
    ApplicationDbContext.cs   # EF model + indexes
    DbSeeder.cs               # migrate + seed starter boards on startup
    DesignTimeDbContextFactory.cs
    Migrations/
  Services/MarkdownRenderer.cs
  Pages/                      # Razor Pages (Index = forum home, Guides/, Identity UI)
  wwwroot/css/classic.css     # the "classic" forum styling
  wwwroot/lib/htmx/
```

# Run commands
```
docker compose up -d db                       # Postgres on :5433
dotnet run --project src/LeagueClassic.Web    # migrates + seeds on first boot
```
