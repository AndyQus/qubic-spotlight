# Qubic Spotlight

![Qubic Spotlight Dashboard](docs/dashboard.png)

Advertising and showcase platform for the Qubic ecosystem. Centrally managed ads
appear on a public Spotlight page (incl. Qubic network metrics) and can be embedded
as an overlay banner into any website via a small code snippet.

Stack: **.NET 10 Blazor Web App** (WebAssembly) · **MudBlazor** · **LiteDB** ·
REST API with **Swagger** · JWT + API key. Built following the pattern of
`qubic_doge_stats`.

> 🇩🇪 Deutsche Version: [README_de.md](README_de.md)

## Why Qubic Spotlight? (The motivation)

The Qubic ecosystem thrives on **Discord**: projects are announced there, across many
channels, working groups, and by now companies and products too. But with so many
channels it's **easy to lose track** — news gets buried, and anyone not on Discord,
or offline for a while, misses it entirely.

Spotlight gathers this news in one place and makes it visible everywhere:

1. **A widget for third-party websites** (the original idea): a single line of code
   embeds the ecosystem's news as a banner/overlay on any site — everyone joins in,
   everyone benefits.
2. **A dedicated public Spotlight page** showing all news in multiple layouts
   (grid, stream, bento, magazine, wall, slide) — with rating (👍/👎), sorting
   (new/top), and filtering by ecosystem group.

Teams and product owners maintain their ads themselves — **directly in the portal or
via the API** from their own applications (create, update, delete).

I provide this web application to the marketing team **free of charge**.

## Project structure

```
qubic_spotlight/            Server (ASP.NET Core, API, LiteDB, Auth, Worker)
qubic_spotlight.Client/     Blazor WASM (Spotlight page, Admin UI)
Shared/                     shared models & DTOs
docs/                       Concept + branding (logo)
Dockerfile, docker-compose.yaml
```

## Running locally

```bash
dotnet run --project qubic_spotlight
```

Then open `http://localhost:5080`. On first start an admin is created automatically
(default `admin@qubic.local` / `changeme`, changeable via the environment variables
`ADMIN_EMAIL` / `ADMIN_PASSWORD`). Login at the top right → "Ads".

Swagger UI: `http://localhost:5080/swagger`

## Admin area — features & roles

The login (top right) leads into the protected area. Which tabs and actions are
visible depends on the user's role.

### Roles & permissions

| Role | Ads | Active-ad limit | Priority / "Pin" | Statistics | User management |
|------|-----|-----------------|------------------|------------|-----------------|
| **Admin** | create / edit / delete all | **unlimited** | ✅ | ✅ | ✅ (users & roles, API keys) |
| **Marketing** | create / edit / delete all | **unlimited** | ✅ | ✅ | ❌ |
| **Ecosystem** | only **own** create / edit / delete | **max. 5 active** | ❌ | ❌ | ❌ |

*Admin + Marketing together form the "Manager" — may do everything without a limit.
Ecosystem partners are automatically pinned to their own ecosystem group on creation
and only see/edit their own ads.*

### How many ads can be created?

- **Ecosystem:** at most **5 simultaneously active** ads. Inactive or expired ads
  don't count. Creating or activating a 6th active ad aborts with a message. The
  limit is configured centrally in `SpotlightLimits.MaxActiveAdsPerOwner` (default
  **5**).
- **Admin / Marketing:** no limit.

### Tabs in the admin area

- **Ads** (all roles): *Active* · *Expired* · **Statistics**
  (clicks / impressions / 👍👎 per period — managers only).
- **Users** (Admin only): create / edit / delete users, assign roles & ecosystem
  group, generate (new) API keys.
- **Embed** (all roles): snippet generator with live preview + copy button.
- **Account** (all): change own password, generate (new) own API key
  (masked preview only; the full key is shown once on creation).

### Prioritization ("Pin", Admin/Marketing only)

An ad can be marked as preferred for a time window; from activation it then takes over
the widget globally for a configurable duration (`PriorityMinutes`, default 30 min),
after which ads rotate normally again.

## API (excerpt)

Public:
- `GET /api/ads` – active ads (for widget/dashboard)
- `GET /api/feed` – sorted ad feed (`sort=new|top`, optional `ecosystem`)
- `GET /api/feed/ecosystems` – available ecosystem groups (for the feed filter)
- `POST /api/ads/{id}/vote` – rate an ad (👍/👎)
- `GET /api/ads/{id}/click` – counts click + redirects
- `POST /api/ads/{id}/impression` – counts impression
- `GET /api/qubic/stats` – Qubic network metrics (cached)
- `GET /api/qubic/blocks` – DOGE/LTC block metrics of the mining pool (cached)
- `GET /api/qubic/price-history` – Qubic price over the last 24h (for the chart)

Authenticated (header `Authorization: Bearer <jwt>` **or** `X-Api-Key: <key>`):
- `GET /api/my/me` – profile of the logged-in user
- `POST /api/my/password` – change own password
- `GET|POST /api/my/ads`, `PUT|DELETE /api/my/ads/{id}` – own ads
- `POST /api/my/apikey` – generate (new) own API key
- `POST /api/uploads` – upload image (≤ 500 KB, PNG/JPG/SVG/WebP)
- `POST /api/auth/login` – login → JWT

Management:
- `/api/admin/ads` (Admin + Marketing) – all ads + `GET /api/admin/ads/stats`
- `/api/admin/users` (Admin only) – user management + API keys

## Embedding on third-party websites

```html
<script src="https://YOUR-HOST/spotlight.js"
        data-mode="slide-panel"   <!-- slide-panel | edge-marquee | corner-card -->
        data-position="right"      <!-- right | left | bottom | top -->
        data-interval="5000"
        data-theme="auto"
        data-max="10"
        data-closable="true"
        async></script>
```

The widget renders itself in the Shadow DOM as its own layer (`position:fixed`) and
does not interfere with the host page's layout. The ready-made snippet is also
available in the admin area under "Embed" (with configuration UI + copy button).

## Docker / Publishing

```bash
docker build -t andyqus/qubic_spotlight:latest .
docker push andyqus/qubic_spotlight:latest
```

On the server (see `docker-compose.yaml`) set before the first start:
`JWT_SECRET` (long random secret), `ADMIN_EMAIL`, `ADMIN_PASSWORD`. Data
(LiteDB file + uploads) lives in the volume under `/data`.

### Deployment for admins (Docker)

The secrets do **not** end up in the image or the repo, but in a `.env` file that
lives exclusively on the server. Steps:

**1. Pull the image**

```bash
docker pull andyqus/qubic_spotlight:latest
```

(Alternatively build it yourself: `docker build -t andyqus/qubic_spotlight:latest .`)

**2. Put `docker-compose.yaml` + `.env.example` on the server**

Both files are in the repo and must reside in the same directory on the server.

**3. Enter secrets** – create the `.env` from the template and fill it in:

```bash
cp .env.example .env
```

```env
JWT_SECRET=<long random secret, min. 32 characters>
ADMIN_EMAIL=admin@qubic.org
ADMIN_PASSWORD=<secure initial password>
```

Generate `JWT_SECRET` e.g. with `openssl rand -base64 48`.

**4. Start**

```bash
docker compose up -d
```

Compose reads the `.env` automatically and passes the values into the container as
environment variables.

**Important for operation:**

- The real `.env` must **not** go into Git (it is in `.gitignore`); only `.env.example`
  is checked in.
- **Fail-fast:** `JWT_SECRET` and `ADMIN_PASSWORD` are marked with `:?` in the compose
  file – if a value is missing, **the start aborts with an error** (by design).
- **Persistent data** lives in the volume mount `/root/spotlight/data` → `/data`. Adjust
  the path to the system if needed (line 18 of the compose file) and back it up.
- The app listens on port `8080` inside the container (mapped to host `8080`). A reverse
  proxy (e.g. Caddy/Nginx) for HTTPS belongs in front of it – the repo contains a
  `Caddy.Dockerfile` as a starting point.
- `ADMIN_EMAIL` / `ADMIN_PASSWORD` take effect **only on the very first start** (while the
  DB is empty). Later changes to them have no effect – the password is then changed via
  the app.

## Configuration (environment variables)

| Variable | Purpose | Default |
|----------|---------|---------|
| `JWT_SECRET` | Signing of the JWTs (min. 32 characters) | dev fallback |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | Initial admin on first start | admin@qubic.local / changeme |
| `DATA_DIR` | Storage for DB + uploads | (local: wwwroot) |
| `LITEDB_FILE` | Name of the DB file | spotlight.db |

## Ideas / Backlog (not yet implemented)

- **Embed an X (Twitter) livestream:** show a livestream started on X on the Spotlight
  page too. X offers **no** official native video embed like YouTube/Twitch. Preferred,
  quick path: render the live post via the official X embed
  (`platform.twitter.com/widgets.js`) as an embedded post card (plays inline, looks
  like a "Twitter card"). For a clean player/fullscreen, alternatively restream to
  YouTube Live/Twitch in parallel and embed from there.
  See [docs/Konzept.md](docs/Konzept.md) ("Ideen / Backlog") for details.

## Notes

- Tracking is GDPR-light: only hashed IP, no plaintext, no external tracking.
  Ratings (👍/👎) use an anonymous identifier generated in the browser (no login required).
- API keys are **not stored in plaintext** (only SHA-256 hash + last 4 characters for
  the masked preview). The full key is returned exclusively once on creation.
- The approval workflow (`Status`) is prepared in the model but inactive in v1
  (all ads visible immediately).
- Package versions (MudBlazor 9.2.0, LiteDB 5.0.21, JwtBearer/WASM 10.0.5,
  Swashbuckle 7.2.0) may need adjusting to the environment via `dotnet restore`.
