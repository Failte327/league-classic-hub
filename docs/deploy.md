# Deploying to production (AWS Lightsail + Route 53)

Target: a single small Lightsail instance running `docker-compose.prod.yml`
(Postgres + the app + Caddy for automatic HTTPS), domain registered and
pointed at it via Route 53. Everything below the "AWS console" steps is
already in the repo — this doc is the runbook for the parts only you can do
(they need your AWS account/payment method).

## 1. Register the domain (Route 53)

1. AWS Console → **Route 53** → **Registered domains** → **Register domain**.
2. Search `leagueclassicarchive.net`, add to cart, complete registration
   (~$13/yr for .net). Route 53 auto-creates a hosted zone for the domain.
3. Leave it as-is for now — the A records get added in step 4 once the
   instance has a static IP.

## 2. Create the Lightsail instance

1. Lightsail console → **Create instance**.
2. Platform: **Linux/Unix** → Blueprint: **OS Only → Ubuntu 24.04 LTS**.
3. Plan: the **$5/mo** tier (1 vCPU, 1 GB RAM, 40 GB SSD, 2 TB transfer) is
   plenty to start.
4. Name it (e.g. `leagueclassic-prod`), create it.
5. Once running: **Networking** tab → **Attach a static IP** (free while
   attached to a running instance). Note the IP.
6. **Networking** tab → firewall rules: keep **SSH (22)**, add **HTTP (80)**
   and **HTTPS (443)**. Remove anything else public (Postgres/5432 and
   Adminer/8080 must never be internet-facing — the prod compose file
   already doesn't publish them, this is just belt-and-suspenders).

## 3. Point DNS at it

Back in Route 53, in the hosted zone for `leagueclassicarchive.net`:

- **A record**, name blank (root), value = the static IP from step 2.5.
- **A record**, name `www`, value = the same static IP.

(Caddy's config already requests a cert covering both `leagueclassicarchive.net`
and `www.leagueclassicarchive.net` — see `Caddyfile` — so both need to resolve
before the first deploy, or that cert request will fail.)

DNS propagation is usually minutes with Route 53; give it up to an hour.

## 4. Set up the instance

SSH in (Lightsail console has a browser SSH button, or use the downloaded key):

```bash
sudo apt-get update && sudo apt-get install -y docker.io docker-compose-v2 git
sudo usermod -aG docker $USER
newgrp docker

git clone <your-repo-url> leagueclassic
cd leagueclassic
cp .env.production.example .env
nano .env   # set a real POSTGRES_PASSWORD; DOMAIN and ACME_EMAIL are already right
```

## 5. First deploy

```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml logs -f web   # watch migrations/seed run, then Ctrl-C
```

Caddy requests the Let's Encrypt cert automatically on first request to port
80/443 — give it a minute, then check `https://leagueclassicarchive.net`.

## 6. Redeploying updates

From the repo on the server:

```bash
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

The app applies pending EF migrations automatically on startup
(`DbSeeder.MigrateAndSeedAsync`), and Data Protection keys are persisted to
the `leagueclassic-dpkeys` volume, so this restart does **not** log everyone
out or break in-flight forms.

## 7. Backups

Two independent options, either is fine to start:

- **Lightsail automatic snapshots** (Snapshots tab, daily, a few cents/GB/mo)
  — backs up the whole instance, simplest.
- **`pg_dump` cron job**: `docker exec leagueclassic-db pg_dump -U leagueclassic leagueclassic | gzip > backup-$(date +%F).sql.gz`,
  shipped somewhere off-box (e.g. an S3 bucket) — more granular, DB-only.

## 8. Optional later: Cloudflare in front

The README's caching plan assumes Cloudflare sits in front for launch (edge
caching on top of the app's own 30s output cache, plus free DDoS/WAF). This
is not required to go live and can be added later at zero cost without
touching the server:

1. Add the site to Cloudflare (free plan).
2. In Route 53, change the domain's nameservers to Cloudflare's (Route 53
   stays the *registrar* — Cloudflare becomes the DNS/proxy layer).
3. Re-create the A records in Cloudflare pointing at the same static IP,
   proxy (orange cloud) on.
4. Set Cloudflare SSL mode to **Full (strict)** — Caddy's cert already makes
   the origin trustworthy, so this avoids the weaker "Flexible" mode.

## Troubleshooting

- **Cert request fails / site won't load over HTTPS**: check
  `docker compose -f docker-compose.prod.yml logs caddy` — almost always a
  DNS record that isn't resolving yet, or firewall blocking 80/443.
- **500s after a deploy**: check `docker compose -f docker-compose.prod.yml logs web`
  first — the EF migration guard means schema drift shows up loudly here
  rather than silently corrupting data.
