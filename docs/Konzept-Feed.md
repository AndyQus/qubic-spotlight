# Qubic Spotlight — Konzept „Feed" (zweite öffentliche Seite)

Neben dem bestehenden **Dashboard** (`/`) bekommt Spotlight eine zweite öffentliche
Seite, auf der **alle Anzeigen wie ein Strom von Neuigkeiten** präsentiert werden —
nicht als Slideshow, sondern zum Stöbern, Sortieren und Bewerten (👍/👎).

Status: Entwurf v0.1 — baut 1:1 auf dem vorhandenen Stack auf
(.NET 10 Blazor Web App, **MudBlazor**, **LiteDB**, Minimal-API, Docker).

> **Kein neues Framework nötig.** Wir bleiben bei MudBlazor + CSS-Grid. Begründung
> siehe Abschnitt 4. Heavy-JS-Feed-Frameworks (React/Vue) würden den Blazor-Stack
> unnötig brechen.

---

## 1. Ziel

Eine Seite, auf der ein Besucher das **ganze Qubic-Ökosystem auf einen Blick**
durchscrollen kann: jede Anzeige ist eine „Neuigkeit" (Tool, Blog, Update). Der
Besucher kann sortieren (neu / beliebt), nach Ecosystem filtern und mit 👍/👎
abstimmen. **Keine Kommentare.**

Wichtig: Eine Zeitung ist nur **eine** mögliche Optik. Wir bauen die Seite so,
dass **mehrere Darstellungs-Varianten über Tabs** umschaltbar sind (dein Wunsch) —
und merken uns die zuletzt gewählte Variante im **localStorage**, damit der Besucher
immer in „seiner" Ansicht landet.

---

## 2. Wie nennen wir die Seite?

Klassisch „News" wirkt altbacken. Vorschläge mit Begründung:

| Name | Route | Charakter |
|------|-------|-----------|
| **Pulse** ⭐ *(Empfehlung)* | `/pulse` | „Der Puls des Ökosystems" — passt zu Live-Stats + Trending. Kurz, modern, brandbar. |
| **Discover** | `/discover` | Entdecker-Gefühl, App-Store-typisch, international verständlich. |
| **Radar** | `/radar` | „Was ist neu auf dem Radar" — Tech-Community-Sound. |
| **Wire** | `/wire` | Anspielung an „Newswire", aber frisch/kurz. |
| **Feed** | `/feed` | Maximal vertraut, aber generisch. |
| **Spotlight Today** | `/today` | An die Marke angelehnt, betont Aktualität. |

**Empfehlung: „Pulse".** Im Header-Nav steht dann z. B. *Dashboard · Pulse*.
„Neuigkeiten" / „News" nur als Untertitel, nicht als Markenname.

---

## 3. Wie präsentieren wir das „cool"? (Tabs als Varianten)

Statt sich auf eine Optik festzulegen, bietet die Seite **4 Ansichten über Tabs**.
Jeder Tab nutzt **dieselben Daten** (die aktiven Ads aus der API) — nur das Layout
ändert sich. Das ist wenig Mehraufwand (gleiche Datenquelle, anderes CSS/Template)
und gibt der Seite sofort einen modernen, verspielten Charakter.

### Tab A — „Stream" (Empfehlung als Standard)
Vertikaler **Social-Feed** wie X/LinkedIn/Product Hunt: pro Anzeige eine breite
Karte (Bild links/oben, Titel, Kurztext, Ecosystem-Chip, 👍/👎, „Öffnen"-Button).
Endloses Scrollen, neueste oder beliebteste zuerst. Wirkt sofort vertraut und
funktioniert auf Mobile perfekt (eine Spalte).

### Tab B — „Bento"
**Bento-Grid** (der aktuelle Trend): Kacheln in unterschiedlichen Größen, große
Hero-Kachel oben, kleinere drumherum. Sehr „2025/2026", visuell, gut für Anzeigen
mit starken Bildern. Reines CSS-Grid, kein JS.

### Tab C — „Magazine"
Die **moderne Zeitung**: oben eine große Aufmacher-Story (das Top-/Pinned-Ad),
darunter eine Editorial-Spaltenanordnung wie bei *The Verge* / digitalen Magazinen.
Bedient dein ursprüngliches „Zeitungs"-Gefühl — aber zeitgemäß, nicht angestaubt.

### Tab D — „Wall" (optional)
**Masonry / Pinterest-Wand**: Karten unterschiedlicher Höhe lückenlos verzahnt.
Sehr lebendig bei vielen Bildern. (Technisch der einzige Tab, der ggf. eine kleine
Hilfsbibliothek braucht — siehe 4.)

> **Tab-Persistenz (dein Wunsch):** Der zuletzt gewählte Tab wird per JS-Interop in
> `localStorage` gespeichert (Key z. B. `qspot_pulse_tab`). Beim erneuten Öffnen der
> Seite liest die Komponente den Wert und aktiviert direkt diese Ansicht. Genau wie
> ihr es schon mit dem Dark-Mode-Key (`qspot_dark`) macht. Zusätzlich merken wir uns
> optional Sortierung (`neu`/`beliebt`) und Ecosystem-Filter im selben Stil.

**Quer über alle Tabs gleich:** oben die bestehende Stats-Leiste (Qubic-Netzwerk-
Werte), darunter eine **Filter-/Sortier-Zeile** (Ecosystem-Chips · Neu · Beliebt ·
Trending), dann die jeweils gewählte Layout-Variante.

---

## 4. „Gibt es ein cooles Open-Source-Framework dafür?" — ehrliche Antwort

Kurz: **Für unseren Blazor-Stack lohnt sich kein fremdes Feed-Framework.** Die
„coolen" Feed-Frameworks (z. B. React-basierte Bibliotheken) gehören in eine
JS-SPA-Welt und würden hier mehr Reibung als Nutzen bringen. Wir erreichen denselben
Look mit Bordmitteln:

- **Stream / Bento / Magazine** → reines **CSS-Grid** (`repeat(auto-fit, …)`,
  `grid-column/row span`). Kein JS, keine Abhängigkeit. Bento-Grids sind heute
  bewusst „nur CSS".
- **Karten, Chips, Tabs, Buttons** → **MudBlazor** liefert ihr schon alles
  (`MudTabs`, `MudCard`, `MudChip`, `MudIconButton`).
- **Masonry-Wand (Tab D)** → CSS `columns` reicht oft. Wer „echtes" Masonry will,
  kann die schlanke Blazor-Interop-Lib **`soenneker.blazor.masonry`** (Wrapper um
  das bekannte Masonry.js) einbinden — nur falls Tab D wirklich gewünscht ist.

Optionale Mini-Bausteine, falls nötig (alle Open Source, CDN):
- **Masonry.js** (24 kB, Klassiker) oder das modernere **„Masonry Grid"** (~1.4 kB)
  für Pinterest-Optik.
- Native CSS `grid-template-rows: masonry` ist noch experimentell → noch nicht für
  Produktion.

**Fazit:** Eigenbau auf MudBlazor + CSS. Maximal eine winzige Masonry-Lib für Tab D.

---

## 5. Vorbilder — welche „coolen" Seiten nachbauen?

| Seite | Was wir übernehmen |
|-------|--------------------|
| **Product Hunt** ⭐ | Das beste Vorbild: täglicher Strom von **Tools mit Upvotes**, nach „beliebt heute" sortiert. Praktisch identisch zu „Anzeigen aus dem Qubic-Ökosystem mit 👍". |
| **Reddit / Hacker News** | 👍/👎 → **Ranking** („Hot"-Sortierung): Score + Zeit-Abfall. Liefert das „Trending"-Gefühl. |
| **The Verge** | Magazin-Tab: mutiger Aufmacher, farbige Akzente, klare Editorial-Spalten. |
| **Dribbble / Behance** | Masonry-Wand mit bildstarken Kacheln, sehr visuell. |
| **App Store „Today" / Flipboard** | „Discover/Today"-Gefühl, kuratierte große Karten. |

**Nordstern = Product Hunt.** Wenn man unsere Seite in einem Satz erklärt:
„Product Hunt fürs Qubic-Ökosystem."

---

## 6. 👍 / 👎 — Bewerten ohne Kommentare

Du willst „Gefällt mir / Gefällt mir nicht", **keine Kommentare**. Das passt exakt
in euer bestehendes Tracking-Muster (`ad_events` mit `IpHash`).

**Datenmodell (minimaler Zusatz):**
- Neuer Event-Typ in `ad_events`: `Like` / `Dislike` (zusätzlich zu
  `Impression` / `Click`). **Kein** neues Schema nötig.
- Denormalisierte Zähler auf `Ad`: `LikeCount`, `DislikeCount` (analog zu den schon
  vorhandenen `ImpressionCount` / `ClickCount`).
- **Anonym & ohne Login:** ein Klick erzeugt ein `ad_events`-Event mit gehashter IP
  (+ optional ein Browser-Token in `localStorage`), damit Mehrfach-Voten desselben
  Besuchers begrenzt wird. Server zählt, UI zeigt den Stand sofort optimistisch an.

**Ranking („Trending"):** Score = `LikeCount − DislikeCount`, kombiniert mit einem
Zeit-Abfall (Reddit-/HN-Style: neue Anzeigen mit gutem Score steigen, alte sinken).
Daraus speist sich der Sortier-Modus **„Beliebt/Trending"**. **„Neu"** = nach
`CreatedAt`. Damit bleibt der Feed dynamisch, ohne dass jemand kuratieren muss.

**API (neue/erweiterte Endpunkte):**
- `GET /api/ads?sort=new|top&ecosystem=…&skip=&take=` → seitenweiser Feed (Paging
  fürs Endlos-Scrollen).
- `POST /api/ads/{id}/vote` `{ value: 1 | -1 }` → registriert Like/Dislike
  (Rate-Limit per IpHash, wie bei Impressions).
- Sichtbarkeitsregel unverändert: `IsActive && Approved && im Zeitfenster`.

---

## 7. Responsive (Desktop & Mobile)

- **Stream:** Desktop max. ~720 px breite Spalte zentriert (lesbar), Mobile volle
  Breite — eine Spalte, große Tap-Targets für 👍/👎.
- **Bento / Magazine / Wall:** `grid-template-columns: repeat(auto-fit, minmax(…))`;
  Desktop 3–4 Spalten, Tablet 2, Mobile 1. Hero-Kachel spannt auf Mobile auf volle
  Breite.
- **Tabs:** Auf Mobile als scrollbare Chip-Leiste (`MudTabs` ist responsiv).
- Gleiches Theme wie Dashboard (Primary `#7e6fff`, Secondary `#4EE0FC`,
  Dark-Default).

---

## 8. Einbettung in den bestehenden Code

Minimal-invasiv, gleiche Muster wie heute:

1. **Neue Seite** `qubic_spotlight.Client/Pages/Pulse.razor` mit `@page "/pulse"`
   (erbt `LocalizedComponentBase`, nutzt `SpotlightApi` wie `Dashboard.razor`).
2. **Nav-Link** in `MainLayout.razor` ergänzen (`@NavLinks` Desktop **und** Drawer),
   z. B. *Dashboard · Pulse*.
3. **MudTabs** mit 4 Panels (Stream/Bento/Magazine/Wall) — jeweils ein
   Render-Fragment über dieselbe `_ads`-Liste.
4. **JS-Interop** für `localStorage` (Tab/Sort/Filter merken) — gleiche Technik wie
   der bestehende `qspot_dark`-Key.
5. **Server:** `ad_events`-Enum um `Like`/`Dislike` erweitern, `Ad` um
   `LikeCount`/`DislikeCount`, `AdService` um Vote- und Paging-Methoden, zwei
   Endpunkte in `ApiEndpoints.cs`.
6. **Übersetzungen** in `Translations.cs` (DE/EN): Tab-Namen, „Neu/Beliebt",
   „Gefällt mir".

---

## 9. Offene Entscheidungen (für dich)

1. **Name** der Seite: *Pulse* (Empfehlung) — oder Discover / Radar / Wire?
2. **Standard-Tab** beim allerersten Besuch: *Stream* (Empfehlung)?
3. **Welche Tabs** bauen wir in v1 — alle vier oder erst *Stream + Bento*?
4. **Voting** komplett anonym (nur IpHash) oder zusätzlich Browser-Token gegen
   Mehrfach-Votes?
5. **Masonry-Tab (Wall)** in v1 dabei (braucht ggf. die kleine Lib) oder später?
