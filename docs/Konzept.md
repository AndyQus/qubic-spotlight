# Qubic Spotlight — Konzept

Werbe- und Showcase-Plattform für das Qubic-Ökosystem. Zentral gepflegte Anzeigen
werden auf einem öffentlichen Dashboard angezeigt und lassen sich per kleinem
Code-Snippet als scrollendes Banner in beliebige Fremd-Webseiten einbinden.

Status: Entwurf v0.2 — abgestimmt auf den vorhandenen Stack der App
`qubic_doge_stats` (gleiche Architektur, gleiches Design, gleiche DB).

> **Basis = `qubic_doge_stats`.** Wir übernehmen dieselbe Projektstruktur
> (Server + `.Client`-WASM + `Shared`), dasselbe MudBlazor-Theme/Logo, **LiteDB**,
> das Minimal-API-Muster und das Docker-Setup. **Neu** gegenüber dieser App:
> ein **Login + Rollensystem** (dort existiert noch keins) und **Swagger**.

---

## 0. Auslöser — warum Qubic Spotlight?

Das Qubic-Ökosystem lebt stark über **Discord**: dort werden Projekte vorgestellt,
es gibt viele Kanäle, Arbeitsgruppen und inzwischen auch Firmen und Produkte. Das
ist großartig — aber man **verliert den Überblick**. Neuigkeiten gehen in der Menge
an Kanälen unter, und wer **nicht auf Discord** ist oder **eine Weile offline** war,
bekommt sie gar nicht mit.

**Qubic Spotlight** löst das mit zwei Bausteinen:

1. **Widget für Fremd-Webseiten (die ursprüngliche Idee):** Mit einer einzigen
   Code-Zeile lassen sich die Neuigkeiten des Ökosystems als Banner/Overlay auf
   beliebigen Seiten einbinden. Wenn alle mitmachen, profitiert jeder — die
   Reichweite jedes Projekts wächst, ohne dass jemand Discord durchsuchen muss.
2. **Eigene öffentliche Seite, chronologisch:** Dieselben Neuigkeiten werden zentral
   und nach Datum sortiert angezeigt — ein Ort, an dem man das ganze Ökosystem auf
   einen Blick durchscrollen kann.

Pflegen können das die Beteiligten selbst: Teams und Produkt-Betreiber legen ihre
Anzeigen (Neuigkeiten/Werbung) **direkt im Portal** an — oder **automatisiert aus
ihren eigenen Anwendungen über die API** (anlegen, ändern, löschen).

Diese Webanwendung stelle ich dem Marketing-Team **kostenfrei** zur Verfügung.

---

## 1. Ziel & Nutzen

- Eine zentrale Stelle, an der das Qubic-Ökosystem seine Tools, Blog-Beiträge und
  Neuigkeiten als "Anzeigen" pflegt.
- Öffentliches Dashboard, das alle aktiven Anzeigen ansprechend zeigt (Übersicht
  + automatische Slideshow alle 5 Sekunden).
- Einbindung in Fremd-Webseiten über ein einziges `<script>`-Snippet — als
  schmaler Banner, der von rechts nach links am Bildschirmrand läuft.
- Rollenbasierte Pflege: Marketing pflegt alles, Ökosystem-Partner nur die
  eigenen Anzeigen.

## 2. Rollen & Rechte

| Rolle              | Anzeigen sehen | Eigene anlegen/ändern/löschen | Alle ändern/löschen | Benutzer/Rollen verwalten |
|--------------------|:--:|:--:|:--:|:--:|
| **Admin**          | ✓ | ✓ | ✓ | ✓ |
| **Marketing**      | ✓ | ✓ | ✓ | – |
| **Ecosystem-Partner** (z. B. Wallet, Explorer, Mining-Tool …) | ✓ | ✓ (nur eigene) | – | – |

Jede Anzeige gehört einem "Owner" (Benutzer) und optional einer "Ecosystem"-Gruppe.
Ein Ecosystem-Partner darf nur Anzeigen bearbeiten, deren Owner er ist. Marketing
und Admin sehen und bearbeiten alles. Die Rechteprüfung erfolgt **server-seitig**
(in der UI und in der API gleichermaßen).

## 3. Datenmodell (LiteDB-Collections)

LiteDB ist schemalos; wir nutzen typisierte POCOs im `Shared`-Projekt, genau wie
in `qubic_doge_stats` (z. B. `PoolBlock`, `EpochSummary`). Collections:

**`ads`** — Anzeige

| Feld          | Typ          | Pflicht | Beschreibung |
|---------------|--------------|:--:|--------------|
| Id            | ObjectId     | ✓ | Primärschlüssel (LiteDB) |
| Title         | string(80)   | ✓ | Überschrift |
| Description   | string(280)  | ✓ | Kurzbeschreibung |
| LinkUrl       | string       | ✓ | Ziel-Link der Anzeige |
| ImageUrl      | string?      | – | Logo/Bild (Upload-Pfad oder externe URL) |
| StartDate     | DateTime     | ✓ | Ab wann sichtbar |
| ExpiryDate    | DateTime?    | – | Optionales Ablaufdatum |
| IsActive      | bool         | ✓ | Manuell de-/aktivierbar |
| Ecosystem     | string?      | – | Gruppe/Projekt (z. B. "Wallet") |
| OwnerUserId   | string       | ✓ | Ersteller (User-Id) |
| Status        | enum         | ✓ | `Approved` / `Pending` / `Rejected` — **vorbereitet** für spätere Freigabe; aktuell immer `Approved` |
| ImpressionCount | long       | – | Denormalisierter Zähler (Anzeigen-Einblendungen) |
| ClickCount    | long         | – | Denormalisierter Zähler (Klicks) |
| CreatedAt / UpdatedAt | DateTime | ✓ | Zeitstempel |

**`users`** — Benutzer

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| Id | ObjectId | Primärschlüssel |
| Email | string | Login (unique Index) |
| PasswordHash | string | PBKDF2 (Admin setzt/ändert Passwörter direkt) |
| ApiKey | string? | Langlebiger API-Key für Automatisierung/CI (regenerierbar) |
| Roles | string[] | `Admin` / `Marketing` / `Ecosystem` |
| Ecosystem | string? | Zugehörige Gruppe bei Ecosystem-Partnern |
| IsActive | bool | Konto aktiv |

**`ad_events`** — Tracking (Klicks & Impressions)

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| Id | ObjectId | Primärschlüssel |
| AdId | string | Bezug zur Anzeige |
| Type | enum | `Impression` / `Click` |
| Timestamp | DateTime | Zeitpunkt |
| IpHash | string? | gehashte IP (kein Klartext) — wie `visitors` in `qubic_doge_stats` |
| Referer | string? | einbindende Domain (optional, für „wo wird's gezeigt") |

Index-Strategie analog `EnsureIndexes()`: `ads` auf `OwnerUserId`, `IsActive`,
`StartDate`; `users` auf `Email` (unique) und `ApiKey`; `ad_events` auf `AdId`
und `Timestamp`.

**Sichtbarkeitsregel (öffentlich):** `IsActive == true` UND
`Status == Approved` UND `StartDate <= heute` UND
(`ExpiryDate == null` ODER `ExpiryDate >= heute`).

**Limit pro Partner:** max. **5 aktive** Anzeigen pro Ecosystem-Partner
(serverseitig geprüft beim Anlegen/Aktivieren; Marketing/Admin unbegrenzt).

## 4. Architektur

Drei logische Teile in **einer** Blazor-Web-App (ein Docker-Image), exakt wie
`qubic_doge_stats` aufgebaut:

```
┌──────────────────────────────────────────────────────────────┐
│  Qubic Spotlight  (Blazor Web App, .NET 10, InteractiveWasm) │
│                                                              │
│  Server-Projekt            .Client (WASM)        Shared      │
│  - Program.cs              - Pages/Components     - Models    │
│  - Endpoints (Minimal API) - Layout (MudBlazor)    (Ad,User) │
│  - Infrastructure/LiteDb   - Services (HttpClient)           │
│  - Auth (Cookie + JWT)                                       │
│                                                              │
│  1) Admin-Bereich   (Login + Rollen)                         │
│     CRUD für Anzeigen + Benutzerverwaltung (MudBlazor)       │
│                                                              │
│  2) Öffentliches Dashboard  (/)                              │
│     Grid aller Anzeigen + Slideshow (5 s)                    │
│                                                              │
│  3) REST-API (Swagger) + Embed-Script                        │
│     GET /api/ads        → öffentliche JSON-Liste (Widget)    │
│     /api/auth/login + /api/my/ads/* → eigene Ads per Token   │
│     GET /swagger        → API-Doku & Test-UI                 │
│     GET /spotlight.js   → schlankes Vanilla-JS-Widget        │
│     GET /embed          → öffentlicher Snippet-Generator      │
│                                                              │
│  LiteDB-Datei im Volume (DATA_DIR=/data)                     │
│  Bild-Uploads → /data/uploads (Volume)                       │
└──────────────────────────────────────────────────────────────┘
```

**Warum so?** Admin und Dashboard laufen als Blazor + MudBlazor (du kennst es,
einfacher C#). Das Banner auf *fremden* Seiten darf aber **kein** Blazor sein —
Fremdseiten laden nur eine kleine JS-Datei. Deshalb liefert die App zusätzlich
eine statische `spotlight.js`, die die öffentliche JSON-API abfragt und den
Banner rendert.

### Technologie-Stack (= bestehende App)
- **.NET 10**, **Blazor Web App**, `InteractiveWebAssembly` (Server + `.Client`)
- **MudBlazor 9.2.0** mit dem vorhandenen Theme (siehe Abschnitt 5)
- **CSS-Klassen** für eigenes Styling (`wwwroot/site.css`, eigene `spotlight.css`)
- **LiteDB 5.0.21** (embedded, `LiteDbContext` als Singleton mit `lock`)
- **Minimal-API** über `app.MapGroup("/api")`, CORS `AllowAnyOrigin`
- **Auth NEU**: Cookie-Auth für den Blazor-Admin + **JWT** für die API
- **Swagger/OpenAPI** (Swashbuckle) unter `/swagger`
- **Docker** (SDK→aspnet, Port 8080, `DATA_DIR`-Volume) → **Docker Hub**

### Auth-Ansatz (Begründung)
`qubic_doge_stats` nutzt **kein** EF Core, sondern LiteDB — daher passt ASP.NET
Core **Identity** (EF-zentriert) schlecht. Stattdessen ein **schlankes eigenes
Auth**: Benutzer in der `users`-Collection, Passwort-Hash (PBKDF2), Login setzt
ein Auth-Cookie (Blazor) bzw. liefert ein **JWT** (API). Rollenprüfung über
Policies / `[Authorize(Roles=…)]`. Einfacher Code, keine zusätzliche DB nötig.

- **Passwörter**: kein Self-Service-Reset in v1 — **Admin setzt/ändert** sie
  direkt im Benutzer-Dialog (E-Mail/SMTP kann später nachgerüstet werden).
- **Konten**: nur Admin/Marketing legen Benutzer an (Rolle + Ecosystem-Gruppe),
  keine Selbstregistrierung.
- **API-Zugang**: jeder Benutzer hat einen **langlebigen, regenerierbaren
  API-Key** (für Automatisierung/CI, da kurzlebiges JWT dort unpraktisch ist).
  Zusätzlich Login per JWT für interaktive Aufrufe.

## 5. Design / Theme (1:1 von `qubic_doge_stats`)

- MudBlazor-Theme: Primary `#7e6fff`, Secondary/Tertiary `#4EE0FC`.
- Dark-Default: Background `#101820`, Appbar `#0d1117`, Surface `#1a2332`,
  Text `#e0e0e0`. Light-Variante analog.
- Header mit Qubic-Logo-SVG (`#23FFFF`) + Titel; Dark-/Light-Umschalter.
- Roboto-Font, `MudBlazor.min.css` + eigenes `site.css`.
- Gleiche `MainLayout`-Struktur (AppBar, MudContainer, Footer-Component).

## 6. Öffentliches Dashboard (`/`)

- Kachel-/Grid-Ansicht: alle aktiven Anzeigen auf einen Blick (Bild,
  Überschrift, Kurztext, Link).
- Darüber eine **Slideshow**, die alle 5 Sekunden zur nächsten Anzeige wechselt
  (MudCarousel oder eigene CSS-Animation).
- Responsiv über CSS-Klassen, im vorhandenen Qubic-Look.

### 6.1 Qubic-Netzwerk-Werte (aus der RPC-API)
Oben auf dem Dashboard eine kompakte Statistik-Leiste mit Live-Werten des Qubic-
Netzwerks — dieselben Kennzahlen wie auf explorer.qubic.org. Datenquelle:
**eine** RPC-Abfrage `GET https://rpc.qubic.org/v1/latest-stats` (plus optional
`v1/tick-info` für den live hochzählenden Tick).

| Kennzahl | Feld (`latest-stats`) | Beispielwert |
|----------|-----------------------|--------------|
| Preis (QU) | `price` | $0.000000491 |
| Market Cap | `marketCap` | $84.11 M |
| Umlaufmenge | `circulatingSupply` | 171.31 T QU |
| Aktive Adressen | `activeAddresses` | 613.176 |
| Aktuelle Epoche | `epoch` | 216 |
| Aktueller Tick | `currentTick` | 55.998.071 |
| Tick-Qualität | `epochTickQuality` | 99.05 % |
| Verbrannte QUs | `burnedQus` | 44.69 T |

Umsetzung analog zum bestehenden Muster in `qubic_doge_stats`
(`QubicRpcClient` + `HttpClient` ist schon da): ein **`QubicStatsClient`** +
**Polling-Worker** (z. B. alle 30–60 s) hält den letzten Wert im Speicher; ein
Endpunkt `GET /api/qubic/stats` liefert ihn ans Dashboard; Darstellung als
MudBlazor-Karten/`MudChip`. So bleibt das öffentliche Dashboard schnell und
belastet die RPC nicht pro Besucher.

**Layout (responsiv):** alle 8 Kacheln auf dem **Desktop in einer Zeile**,
auf **Mobile untereinander**. Umsetzung per CSS-Grid mit auto-fit, z. B.:

```css
.qs-stats {
  display: grid;
  grid-template-columns: repeat(8, 1fr);   /* Desktop: 8 in einer Reihe */
  gap: 8px;
}
@media (max-width: 1100px) { .qs-stats { grid-template-columns: repeat(4, 1fr); } }
@media (max-width: 700px)  { .qs-stats { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 420px)  { .qs-stats { grid-template-columns: 1fr; } }  /* Mobile: untereinander */
```

Jede Kachel kompakt: kleines Label oben, Wert groß darunter; Zahlen kompakt
formatiert (z. B. „171.31T", „$84.11M", „99.05%"), damit acht nebeneinander
passen. Optional kann der Tick live mitzählen (Timer + `tick-info`).

## 7. Einbindung in Fremd-Webseiten (Kern-Feature)

Der Betreiber einer fremden Seite fügt **eine Zeile** in sein HTML ein:

```html
<script src="https://spotlight.qubic.org/spotlight.js"
        data-mode="slide-panel"   <!-- slide-panel | edge-marquee | corner-card -->
        data-position="right"      <!-- right | left | bottom | top -->
        data-interval="5000"       <!-- ms pro Anzeige (Auto-Wechsel) -->
        data-speed="40"            <!-- Tempo bei edge-marquee -->
        data-theme="auto"          <!-- auto | dark | light -->
        data-max="10"              <!-- max. Anzeigen -->
        data-closable="true"       <!-- Schließen-Button anzeigen -->
        async></script>
```

### Eigener Overlay-Layer, nicht ins Seitenlayout eingreifend
Das Widget rendert sich als **eigene Ebene über** der Fremdseite:
`position: fixed`, hoher `z-index`, gekapselt im **Shadow DOM**. Dadurch
- greift es **nicht** in das Layout/den Flow der Fremdseite ein,
- ist das CSS beidseitig isoliert (Fremdseite verbiegt das Widget nicht, und
  unser CSS beeinflusst die Fremdseite nicht).

### Der Betreiber entscheidet die Darstellung (`data-`Attribute)
Da jede Webseite anders ist, legt der Betreiber Erscheinung und Verhalten selbst
fest — gleiches Script, nur andere Attribute:

- **`slide-panel`** — Panel fährt vom gewählten Rand (z. B. rechts) als Overlay
  herein (CSS-`transform`), zeigt die Anzeigen, wechselt alle `interval` ms,
  schließbar. *(Default)*
- **`edge-marquee`** — schmaler Streifen am Rand, Anzeigen laufen **von rechts
  nach links** durch (`speed` steuert das Tempo).
- **`corner-card`** — kleine Karte in einer Bildschirmecke, Auto-Wechsel.

Sinnvolle Defaults: ein nacktes `<script src="…spotlight.js" async></script>`
sieht ohne weitere Attribute bereits gut aus (slide-panel, rechts, 5 s,
schließbar).

### Ablauf von `spotlight.js`
1. lädt von `https://spotlight.qubic.org/api/ads` die aktiven Anzeigen,
2. liest die `data-`Attribute des eigenen Script-Tags (Config),
3. baut den gewählten Overlay-Layer (Shadow DOM) auf,
4. wechselt/scrollt die Anzeigen automatisch; jede ist als Link klickbar.

Vorteile: keine Abhängigkeit zur Technik der Fremdseite, kein iframe-Zwang,
DSGVO-arm (kein Tracking nötig). Das fertige, vorkonfigurierte Snippet wird auf
der öffentlichen Seite `/embed` zum Kopieren angezeigt (mit Live-Vorschau der
Optionen).

## 8. API (REST + Swagger)

Zwei Bereiche, beide unter `/swagger` dokumentiert und testbar.

### 8.1 Öffentlich (kein Token)
```
GET /api/ads
→ 200 OK
[ { "id":"…", "title":"Qubic Wallet 2.0", "description":"…",
    "linkUrl":"https://wallet.qubic.org",
    "imageUrl":"https://spotlight.qubic.org/uploads/wallet.png" } ]
```
Nur öffentlich sichtbare Anzeigen (Filter aus Abschnitt 3). CORS `*`, optional
Caching (~60 s).

### 8.2 Authentifiziert — eigene Anzeigen verwalten
Interne Benutzer verwalten ihre **eigenen** Anzeigen per API (ohne UI). Zwei
Auth-Wege, beide nutzbar:
- **API-Key** (langlebig, für CI/Automatisierung): Header `X-Api-Key: <key>`.
- **JWT** (interaktiv): `POST /api/auth/login` → Bearer-Token.

```
POST   /api/auth/login        { email, password }     → { token, expiresAt }

# Auth-Header (eine Variante):  Authorization: Bearer <jwt>   ODER   X-Api-Key: <key>
GET    /api/my/ads            → eigene Anzeigen (Owner == aktueller User)
GET    /api/my/ads/{id}       → eigene Einzel-Anzeige
POST   /api/my/ads            → anlegen (Owner = aktueller User, server-seitig)
PUT    /api/my/ads/{id}       → ändern (nur eigene)
DELETE /api/my/ads/{id}       → löschen (nur eigene)
```
Regeln:
- Owner wird **server-seitig** aus Token/Key gesetzt; fremde Anzeige ⇒ `403`.
- **Marketing/Admin** dürfen zusätzlich `/api/ads/{id}` (alle Anzeigen).
- 5-Anzeigen-Limit für Ecosystem-Partner wird auch hier geprüft.
- Swagger-UI mit "Authorize"-Button (Bearer **und** ApiKey) zum direkten Testen.

### 8.3 Tracking-Endpunkte (öffentlich)
```
POST /api/ads/{id}/impression   → zählt eine Einblendung (Beacon, kein Body)
GET  /api/ads/{id}/click        → zählt Klick und leitet per 302 auf LinkUrl
```
Das Embed-Widget meldet Impressions und nutzt für Links den `…/click`-Redirect,
damit Klicks gezählt werden. Speicherung in `ad_events` + Hochzählen der
Denormal-Zähler an der Anzeige.

## 9. Analytics (Klicks & Impressions)

- Jede Einblendung (Impression) und jeder Klick wird in `ad_events` erfasst und
  an der Anzeige hochgezählt (siehe 8.3).
- **DSGVO-arm**: keine personenbezogenen Daten — nur gehashte IP (wie die
  bestehende `visitors`-Logik in `qubic_doge_stats`), Zeitstempel, optional die
  einbindende Domain (Referer).
- **Admin-Auswertung**: pro Anzeige Impressions/Klicks + CTR, Zeitverlauf
  (Tag/Monat) und „auf welchen Domains wird's gezeigt". Darstellung mit
  **Blazor-ApexCharts** (ist im Stack schon vorhanden).
- Ecosystem-Partner sehen die Statistik **ihrer eigenen** Anzeigen.

## 10. Freigabe / Moderation (vorbereitet, in v1 inaktiv)

- Feld `Status` (`Approved`/`Pending`/`Rejected`) ist im Datenmodell vorhanden.
- v1: neue Anzeigen sind **sofort `Approved`** (kein Freigabe-Schritt).
- Per Konfiguration `RequireApproval` (env, Default `false`) lässt sich später
  ein Workflow aktivieren: Partner-Anzeigen starten als `Pending`, Marketing/
  Admin gibt frei. UI und Sichtbarkeitsregel sind dafür schon ausgelegt.

## 11. Sicherheit
- Login nur für den geschlossenen Bereich; Public-Teile ohne Login.
- Rollenprüfung server-seitig bei jedem CRUD-Aufruf (nicht nur im UI).
- Ecosystem-Partner: Schreibrechte nur auf eigene Datensätze (Owner-Check) —
  gilt für UI **und** API; max. 5 aktive Anzeigen pro Partner.
- Passwort-Hashing (PBKDF2), JWT signiert mit Server-Secret; API-Key langlebig
  und regenerierbar (Widerruf = neuer Key).
- Upload-Validierung: **PNG/JPG/SVG/WebP**, **≤ 500 KB**; Bilder im Volume
  `/data/uploads`. SVG wird sanitized (kein eingebettetes Script).

## 12. Deployment (= Muster der bestehenden App)
- Ein `dockerfile` (SDK 10 build → aspnet 10 runtime), Port 8080.
- `docker-compose.yaml` mit Volume für LiteDB + Uploads (`/data`).
- Env-Variablen: `DATA_DIR`, `LITEDB_FILE`, `JWT_SECRET`, `ADMIN_EMAIL`,
  `ADMIN_PASSWORD` (Initial-Admin beim ersten Start anlegen), `BASE_URL`.
- Image z. B. `andyqus/qubic_spotlight:latest` → Docker Hub; Core-Team zieht es.

## 13. Geplante Projektstruktur

```
qubic_spotlight/
├─ docs/
│  └─ Konzept.md            ← dieses Dokument
├─ qubic_spotlight/         (Server-Projekt)
│  ├─ Components/           (App.razor, Routes, Admin-Pages, Dialoge)
│  ├─ Endpoints/            (ApiEndpoints.cs: /api/ads, /api/my/ads, /api/auth)
│  ├─ Infrastructure/       (LiteDbContext, Auth-Helper)
│  ├─ Services/             (AdService, UserService, JwtService)
│  ├─ wwwroot/              (site.css, spotlight.css, spotlight.js, uploads/)
│  └─ Program.cs
├─ qubic_spotlight.Client/  (WASM: Dashboard, Admin-UI, Layout, Services)
├─ Shared/Models/           (Ad, User, DTOs)
├─ dockerfile
├─ docker-compose.yaml
└─ README.md
```

## 14. Umsetzungs-Roadmap

1. **Konzept finalisieren** (offene Fragen) ← *wir sind hier*
2. Solution-Gerüst nach Vorbild `qubic_doge_stats` (Server + Client + Shared)
3. LiteDB-Context + Models (`Ad`, `User`, `AdEvent`) + Auth (Cookie/JWT/API-Key) + Rollen
4. Admin-UI: Anzeigenliste, Anlegen/Bearbeiten/Löschen, Benutzerverwaltung
5. Öffentliches Dashboard + Slideshow
6. API `/api/ads`, `/api/my/ads`, `/api/auth`, Tracking-Endpunkte + **Swagger**
7. `spotlight.js` Embed-Widget (Overlay/Shadow-DOM) + Copy-Snippet im Admin
8. Analytics-Auswertung (ApexCharts) für Klicks/Impressions
9. dockerfile + docker-compose + Docker-Hub-Veröffentlichung
10. README/Doku

---

## Entschieden ✅

- **Auth**: schlankes eigenes Auth auf LiteDB (Cookie + JWT + langlebiger API-Key).
- **Passwörter**: Admin setzt direkt; keine Selbstregistrierung.
- **Bilder**: Upload **und** externe URL; PNG/JPG/SVG/WebP, ≤ 500 KB.
- **Banner**: konfigurierbar (`data-`Attribute), Default `slide-panel` von rechts.
- **Limit**: 5 aktive Anzeigen pro Ecosystem-Partner.
- **Freigabe**: in v1 inaktiv, aber im Modell/UI vorbereitet (`RequireApproval`).
- **Tracking**: Klicks + Impressions von Anfang an (ApexCharts-Auswertung).
- **Projekt**: eigene Solution/Image `qubic_spotlight`, Design 1:1 übernommen.
- **Logo**: eigenes Spotlight-Logo (siehe `docs/branding/`).

## Noch offen (kein Blocker fürs Gerüst)

1. **Ecosystem-Start-Gruppen**: konkrete Liste (z. B. Wallet, Explorer,
   DOGE Stats, Mining-Pool …) — Admin-pflegbar, du nennst mir die Startwerte.
2. **Sprache**: einsprachig Englisch für v1 (wie qubic.org)? Mehrsprachigkeit
   später optional.
