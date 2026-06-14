# Qubic Spotlight

Werbe- und Showcase-Plattform für das Qubic-Ökosystem. Zentral gepflegte Anzeigen
erscheinen auf einem öffentlichen Dashboard (inkl. Qubic-Netzwerk-Kennzahlen) und
lassen sich per kleinem Code-Snippet als Overlay-Banner in beliebige Webseiten
einbinden.

Stack: **.NET 10 Blazor Web App** (WebAssembly) · **MudBlazor** · **LiteDB** ·
REST-API mit **Swagger** · JWT + API-Key. Aufgebaut nach dem Muster von
`qubic_doge_stats`.

## Projektstruktur

```
qubic_spotlight/            Server (ASP.NET Core, API, LiteDB, Auth, Worker)
qubic_spotlight.Client/     Blazor WASM (Dashboard, Admin-UI)
Shared/                     gemeinsame Models & DTOs
docs/                       Konzept + Branding (Logo)
dockerfile, docker-compose.yaml
```

## Lokal starten

```bash
dotnet run --project qubic_spotlight
```

Dann `http://localhost:5080` öffnen. Beim ersten Start wird automatisch ein Admin
angelegt (Standard `admin@qubic.local` / `changeme`, über Umgebungsvariablen
`ADMIN_EMAIL` / `ADMIN_PASSWORD` änderbar). Login oben rechts → „Anzeigen".

Swagger-UI: `http://localhost:5080/swagger`

## Rollen

| Rolle | Rechte |
|-------|--------|
| `Admin` | alles + Benutzerverwaltung |
| `Marketing` | alle Anzeigen pflegen/löschen |
| `Ecosystem` | nur eigene Anzeigen (max. 5 aktive) |

## API (Auszug)

Öffentlich:
- `GET /api/ads` – aktive Anzeigen (fürs Widget/Dashboard)
- `GET /api/qubic/stats` – Qubic-Netzwerk-Kennzahlen (gecacht)
- `GET /api/ads/{id}/click` – zählt Klick + leitet weiter
- `POST /api/ads/{id}/impression` – zählt Einblendung

Authentifiziert (Header `Authorization: Bearer <jwt>` **oder** `X-Api-Key: <key>`):
- `GET|POST /api/my/ads`, `PUT|DELETE /api/my/ads/{id}` – eigene Anzeigen
- `POST /api/my/apikey` – eigenen API-Key (neu) erzeugen
- `POST /api/auth/login` – Login → JWT

Verwaltung: `/api/admin/ads` (Admin+Marketing), `/api/admin/users` (Admin).

## Einbinden auf Fremd-Webseiten

```html
<script src="https://DEIN-HOST/spotlight.js"
        data-mode="slide-panel"   <!-- slide-panel | edge-marquee | corner-card -->
        data-position="right"      <!-- right | left | bottom | top -->
        data-interval="5000"
        data-theme="auto"
        data-max="10"
        data-closable="true"
        async></script>
```

Das Widget rendert sich im Shadow DOM als eigene Ebene (`position:fixed`) und greift
nicht ins Layout der Fremdseite ein. Das fertige Snippet gibt es auch im Admin unter
„Embed" (mit Konfigurations-UI + Copy-Button).

## Docker / Veröffentlichung

```bash
docker build -t andyqus/qubic_spotlight:latest -f dockerfile .
docker push andyqus/qubic_spotlight:latest
```

Auf dem Server (siehe `docker-compose.yaml`) vor dem ersten Start setzen:
`JWT_SECRET` (langes Zufallsgeheimnis), `ADMIN_EMAIL`, `ADMIN_PASSWORD`. Daten
(LiteDB-Datei + Uploads) liegen im Volume unter `/data`.

## Konfiguration (Umgebungsvariablen)

| Variable | Zweck | Default |
|----------|-------|---------|
| `JWT_SECRET` | Signatur der JWTs (min. 32 Zeichen) | dev-Fallback |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | Initial-Admin beim ersten Start | admin@qubic.local / changeme |
| `DATA_DIR` | Ablage für DB + Uploads | (lokal: wwwroot) |
| `LITEDB_FILE` | Name der DB-Datei | spotlight.db |

## Hinweise

- Tracking ist DSGVO-arm: nur gehashte IP, kein Klartext, kein externes Tracking.
- Freigabe-Workflow (`Status`) ist im Modell vorbereitet, in v1 aber inaktiv
  (alle Anzeigen sofort sichtbar).
- Paketversionen (MudBlazor 9.2.0, LiteDB 5.0.21, JwtBearer/WASM 10.0.5,
  Swashbuckle 7.2.0) ggf. per `dotnet restore` an die Umgebung anpassen.
