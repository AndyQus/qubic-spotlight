# Qubic Spotlight – Sicherer VPS von Null auf (Schritt für Schritt)

Diese Anleitung bringt **Qubic Spotlight** auf einem frischen Ubuntu-VPS produktiv ans
Netz – mit eigener Domain, automatischem HTTPS und einer soliden Sicherheits-Baseline.

**Setup-Entscheidungen dieser Anleitung:** Ubuntu 24.04 LTS · Image wird von Docker
Hub gezogen (kein Build auf dem Server) · Reverse Proxy **Caddy** (automatisches
Let's-Encrypt-TLS).

**Zeitaufwand:** ca. 1–2 Stunden. Befehle der Reihe nach abarbeiten.

> Konvention: `dein-server` = öffentliche IP des VPS, `spotlight.deine-domain.tld`
> = die Domain, unter der die App laufen soll. Ersetze diese überall durch deine Werte.

---

## 0. Voraussetzungen / Was du brauchst

- **VPS** mit Ubuntu 24.04 LTS. Empfehlung: **2 vCPU, 2 GB RAM, 40 GB SSD**
  (z. B. Hetzner CX22). Minimum 1 GB RAM. LiteDB ist eingebettet – keine separate
  Datenbank nötig.
- **Eine Domain** bei einem Registrar (Namecheap, Cloudflare, INWX, Porkbun …).
- **SSH-Key-Paar** auf deinem PC (kein Passwort-Login!). Falls du noch keinen hast:

  ```bash
  # Auf DEINEM Rechner (nicht auf dem Server) ausführen:
  ssh-keygen -t ed25519 -C "qubic-spotlight"
  # Enter drücken (Standardpfad ~/.ssh/id_ed25519), starke Passphrase vergeben.
  ```

- **Docker-Hub-Image** `andyqus/qubic_spotlight:latest` muss gepusht sein. Falls noch
  nicht geschehen, lokal auf deinem Entwicklungsrechner:

  ```bash
  docker build -t andyqus/qubic_spotlight:latest .
  docker login
  docker push andyqus/qubic_spotlight:latest
  ```

---

## 1. VPS bestellen und ersten Zugang einrichten

1. Beim Anbieter einen Server mit **Ubuntu 24.04 LTS** anlegen.
2. **Wichtig:** beim Anlegen deinen **SSH-Public-Key** (`~/.ssh/id_ed25519.pub`)
   hinterlegen. Dann ist von Anfang an kein Passwort-Login nötig.
3. Du bekommst eine **öffentliche IP**. Erstmals verbinden:

   ```bash
   ssh root@dein-server
   ```

---

## 2. System aktualisieren

```bash
apt update && apt upgrade -y
apt install -y curl ufw fail2ban unattended-upgrades ca-certificates gnupg
```

---

## 3. Eigenen Admin-Benutzer anlegen (nicht als root arbeiten)

```bash
adduser andy                       # Passwort vergeben
usermod -aG sudo andy              # sudo-Rechte geben

# SSH-Key vom root-Login für den neuen User übernehmen:
rsync --archive --chown=andy:andy ~/.ssh /home/andy
```

Jetzt in **neuem Terminal** testen, ob der Login als neuer User funktioniert:

```bash
ssh andy@dein-server
```

Erst weitermachen, wenn das klappt – sonst sperrst du dich später aus.

---

## 4. SSH härten

Als `andy` auf dem Server:

```bash
sudo nano /etc/ssh/sshd_config
```

Folgende Werte setzen bzw. anpassen:

```
PermitRootLogin no
PasswordAuthentication no
PubkeyAuthentication yes
ChallengeResponseAuthentication no
UsePAM no
X11Forwarding no
```

Speichern, dann SSH neu laden:

```bash
sudo systemctl restart ssh
```

> Optional, aber wirksam gegen Bot-Scans: SSH auf einen anderen Port legen
> (z. B. `Port 2222` in derselben Datei). Dann unten in der Firewall denselben Port
> öffnen und mit `ssh -p 2222 andy@dein-server` verbinden.

**Lass das aktuelle Terminal offen** und teste den Login in einem zweiten Fenster,
bevor du das erste schließt.

---

## 5. Firewall (ufw) einrichten

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow OpenSSH          # bzw. 'sudo ufw allow 2222/tcp' bei geändertem Port
sudo ufw allow 80/tcp           # HTTP (Let's-Encrypt-Challenge + Redirect)
sudo ufw allow 443/tcp          # HTTPS
sudo ufw enable
sudo ufw status verbose
```

Nur 22 (SSH), 80 und 443 sind offen. Port 8080 der App bleibt **dicht** – darauf
greift nur der Reverse Proxy intern zu.

---

## 6. fail2ban (Brute-Force-Schutz) aktivieren

```bash
sudo tee /etc/fail2ban/jail.local > /dev/null <<'EOF'
[sshd]
enabled = true
maxretry = 4
bantime = 1h
findtime = 10m
EOF

sudo systemctl enable --now fail2ban
sudo systemctl restart fail2ban
sudo fail2ban-client status sshd
```

---

## 7. Automatische Sicherheitsupdates

```bash
sudo dpkg-reconfigure -plow unattended-upgrades   # Dialog mit "Ja" bestätigen
```

Prüfen, dass Auto-Updates aktiv sind:

```bash
cat /etc/apt/apt.conf.d/20auto-upgrades
# sollte enthalten:
# APT::Periodic::Update-Package-Lists "1";
# APT::Periodic::Unattended-Upgrade "1";
```

---

## 8. Docker & Docker Compose installieren

Offizielle Docker-Quelle einrichten und installieren:

```bash
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | \
  sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Den eigenen User in die docker-Gruppe (kein sudo mehr nötig für docker):
sudo usermod -aG docker andy
```

Danach einmal aus- und wieder einloggen (`exit`, dann erneut `ssh andy@dein-server`),
damit die Gruppe greift. Test:

```bash
docker --version
docker compose version
docker run --rm hello-world
```

---

## 9. Domain auf den Server zeigen lassen

Beim Registrar/DNS-Anbieter zwei Records anlegen (Werte: deine Server-IP):

| Typ    | Name                    | Wert            |
|--------|-------------------------|-----------------|
| `A`    | `spotlight` (oder `@`)  | `dein-server`   |
| `AAAA` | `spotlight`             | IPv6 (falls vorhanden) |

DNS-Verbreitung prüfen (kann ein paar Minuten bis Stunden dauern):

```bash
dig +short spotlight.deine-domain.tld
# muss deine Server-IP zurückgeben
```

Caddy holt das TLS-Zertifikat **erst, wenn der DNS stimmt** – also vorher abwarten.

---

## 10. Projektordner und Konfiguration anlegen

```bash
sudo mkdir -p /opt/qubic_spotlight
sudo chown andy:andy /opt/qubic_spotlight
cd /opt/qubic_spotlight
mkdir -p data caddy_data caddy_config
```

### 10a. docker-compose.yaml

```bash
nano /opt/qubic_spotlight/docker-compose.yaml
```

Inhalt:

```yaml
services:
  qubic_spotlight:
    image: andyqus/qubic_spotlight:latest
    container_name: qubic_spotlight
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - ASPNETCORE_ENVIRONMENT=Production
      - DATA_DIR=/data
      - LITEDB_FILE=spotlight.db
      - JWT_SECRET=${JWT_SECRET:?JWT_SECRET fehlt in .env}
      - ADMIN_EMAIL=${ADMIN_EMAIL:-admin@qubic.org}
      - ADMIN_PASSWORD=${ADMIN_PASSWORD:?ADMIN_PASSWORD fehlt in .env}
      # Optional: Länder-Statistik. Pfad zur lokalen GeoLite2-Country-DB.
      # Fehlt die Datei, werden Besuche einfach ohne Land gezählt (siehe 10d).
      - GEOIP_DB=/data/GeoLite2-Country.mmdb
    volumes:
      - ./data:/data
    # KEIN ports:-Mapping nach außen! Nur Caddy spricht intern mit der App.
    expose:
      - "8080"
    restart: unless-stopped

  caddy:
    # Eigenes Image mit Rate-Limit-Modul (siehe Caddy.Dockerfile).
    # Ohne Rate-Limiting genügt stattdessen: image: caddy:2
    build:
      context: .
      dockerfile: Caddy.Dockerfile
    image: qubic_caddy:latest
    container_name: caddy
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - ./caddy_data:/data
      - ./caddy_config:/config
    depends_on:
      - qubic_spotlight
    restart: unless-stopped
```

> Die Datei `Caddy.Dockerfile` liegt im Projekt-Root und muss mit auf den Server (in
> `/opt/qubic_spotlight/`). Sie baut Caddy einmalig mit dem `caddy-ratelimit`-Modul.

> Sicherheitsvorteil: Die App hat **kein** `ports:`-Mapping mehr. Sie ist nur im
> internen Docker-Netz unter `qubic_spotlight:8080` erreichbar. Von außen kommt man
> ausschließlich über Caddy (443) rein.

### 10b. Caddyfile (automatisches HTTPS)

```bash
nano /opt/qubic_spotlight/Caddyfile
```

Inhalt:

```
{
    # Reihenfolge für das Rate-Limit-Modul festlegen.
    order rate_limit before basicauth
}

spotlight.deine-domain.tld {
    encode gzip zstd

    # === CORS für die öffentlichen Widget-Endpunkte ===
    # Das Widget (spotlight.js) läuft auf FREMDEN Domains und ruft per fetch()
    # diese Endpunkte cross-origin auf -> sie brauchen CORS-Header.
    # Nur öffentliche Lese-/Zähl-Endpunkte freigeben, NICHT /api/my oder /api/admin.
    @public_api path /api/ads* /api/qubic/*
    @preflight {
        method OPTIONS
        path /api/ads* /api/qubic/*
    }

    # Preflight-Anfragen (OPTIONS) sofort beantworten:
    handle @preflight {
        header Access-Control-Allow-Origin "*"
        header Access-Control-Allow-Methods "GET, POST, OPTIONS"
        header Access-Control-Allow-Headers "Content-Type"
        header Access-Control-Max-Age "86400"
        respond 204
    }
    # CORS-Header auf die echten Antworten der öffentlichen Endpunkte:
    header @public_api Access-Control-Allow-Origin "*"

    # === Rate Limiting (benötigt caddy-ratelimit, siehe Caddy.Dockerfile) ===
    # Pro Client-IP max. 600 Anfragen je Minute auf /api/* (= 10/s im Schnitt).
    # Großzügig genug für viele Besucher hinter einer NAT-/CGNAT-IP, stoppt aber
    # Bots/Scraper, die mit tausenden Requests/s hämmern. Bei 429 = Limit erreicht.
    rate_limit {
        zone api {
            match {
                path /api/*
            }
            key    {remote_host}
            events 600
            window 1m
        }
    }

    # === Sicherheits-Header (gelten für alle Antworten) ===
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "strict-origin-when-cross-origin"
    }

    reverse_proxy qubic_spotlight:8080
}
```

**Erläuterung der Entscheidungen:**

- **CORS mit `*` nur für öffentliche Endpunkte** (`/api/ads*`, `/api/qubic/*`): Das
  Widget kann auf beliebigen Fremd-Domains liegen, daher ist Wildcard hier korrekt.
  Diese Endpunkte tragen keine Cookies/Credentials, also unkritisch. Die
  authentifizierten Pfade (`/api/my`, `/api/admin`, `/api/auth`) bekommen **bewusst
  keine** CORS-Freigabe – Admin-/Marketing-UI läuft same-origin.
- **Kein `X-Frame-Options` mehr**: Das Widget arbeitet im Shadow DOM (kein iframe),
  also nicht nötig. Falls du die App-Seite selbst gegen Framing schützen willst, kannst
  du es wieder ergänzen.
- **Rate-Limit `120/min` pro IP** ist ein konservativer Startwert gegen Bots/Burst.
  Bei Bedarf hochsetzen (mehrere Nutzer hinter einer NAT-IP teilen sich das Limit).
  Schlägt es zu, antwortet Caddy mit HTTP 429.

Caddy besorgt für die Domain automatisch ein Let's-Encrypt-Zertifikat und erneuert es
selbstständig. HTTP wird automatisch auf HTTPS umgeleitet.

### 10c. .env mit den Geheimnissen

```bash
nano /opt/qubic_spotlight/.env
```

Inhalt:

```ini
JWT_SECRET=HIER_EINSETZEN
ADMIN_EMAIL=admin@qubic.org
ADMIN_PASSWORD=HIER_EIN_TEMPORAERES_STARTPASSWORT
```

`JWT_SECRET` bequem erzeugen und Rechte einschränken:

```bash
echo "JWT_SECRET=$(openssl rand -base64 48)" >> /opt/qubic_spotlight/.env
chmod 600 /opt/qubic_spotlight/.env
```

> `ADMIN_PASSWORD` wird nur beim **allerersten** Start verwendet (solange die DB leer
> ist). Nach dem ersten Login in der App das Passwort dort ändern.

### 10d. Länder-Statistik (optional, GeoLite2)

Im Admin-Bereich (Tab **Besucher**) gibt es eine Liste „Besuche nach Ländern". Das
Land wird **nur zum Zeitpunkt des Aufrufs** aus der IP ermittelt; die **IP wird nie
gespeichert** — abgelegt wird ausschließlich der anonymisierte 2-Buchstaben-Ländercode
(z. B. `DE`). Ohne die DB funktioniert alles weiter, die Besuche zählen dann nur ohne
Land (`Unbekannt`).

Die Erkennung nutzt die kostenlose, **offline** arbeitende MaxMind-Datenbank
`GeoLite2-Country.mmdb` (keine externen Aufrufe zur Laufzeit):

```bash
# 1) Kostenlosen MaxMind-Account anlegen: https://www.maxmind.com/en/geolite2/signup
# 2) Lizenzschlüssel erzeugen (Account → Manage License Keys)
# 3) GeoLite2-Country (Format: MaxMind DB, .mmdb) herunterladen, z. B.:
LICENSE_KEY=DEIN_KEY
curl -L -o /tmp/geolite2.tar.gz \
  "https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-Country&license_key=${LICENSE_KEY}&suffix=tar.gz"

# 4) Die .mmdb ins Daten-Volume legen (neben spotlight.db):
tar -xzf /tmp/geolite2.tar.gz -C /tmp
cp /tmp/GeoLite2-Country_*/GeoLite2-Country.mmdb /opt/qubic_spotlight/data/
docker compose restart qubic_spotlight
```

Der Pfad ist über `GEOIP_DB` (Standard `/data/GeoLite2-Country.mmdb`) gesetzt; die App
lädt die DB beim Start. Im Log erscheint `GeoLite2-Country-DB geladen: …` bzw. der
Hinweis, dass keine DB gefunden wurde.

> Wichtig für die korrekte IP: Caddy gibt die echte Besucher-IP im Header
> `X-Forwarded-For` an die App weiter (Standardverhalten von `reverse_proxy`). Nur diese
> wird flüchtig zur Länderermittlung benutzt. MaxMind verlangt, die DB regelmäßig zu
> aktualisieren — ein wöchentlicher Cron mit obigem Download genügt.

---

## 11. Starten

```bash
cd /opt/qubic_spotlight
docker compose pull              # zieht das App-Image von Docker Hub
docker compose up -d --build     # --build: baut einmalig das Caddy-Image mit Rate-Limit
docker compose logs -f           # "Now listening on" = App läuft; Caddy holt das Zertifikat
```

> Der erste `--build` dauert ein paar Minuten (Caddy wird mit dem Modul kompiliert).
> Bei späteren Starts ist es sofort da. App-Updates (Abschnitt 13) brauchen kein
> `--build`, nur wenn du das Caddyfile/Caddy.Dockerfile änderst.

Mit `Strg+C` aus dem Log aussteigen (Container laufen weiter).

Aufruf im Browser: **`https://spotlight.deine-domain.tld`** → Login oben rechts mit
`ADMIN_EMAIL` / `ADMIN_PASSWORD`.

Swagger-UI: `https://spotlight.deine-domain.tld/swagger`

---

## 12. Erste Schritte nach dem Login (Pflicht)

1. In der App **Admin-Passwort ändern**.
2. `ADMIN_PASSWORD` in der `.env` leeren (Wert ist danach bedeutungslos).
3. Erste Anzeige anlegen und das **Embed-Snippet** unter „Embed" testen – auf einer
   Testseite einbinden:

   ```html
   <script src="https://spotlight.deine-domain.tld/spotlight.js"
           data-mode="slide-panel" data-position="right" async></script>
   ```

---

## 13. Updates einspielen

Wenn ein neues Image auf Docker Hub liegt:

```bash
cd /opt/qubic_spotlight
docker compose pull
docker compose up -d
```

Daten (Benutzer, Anzeigen, Bilder) bleiben erhalten – sie liegen im Volume `./data`.

---

## 14. Backups

Es reicht, das Datenverzeichnis zu sichern (LiteDB-Datei + Uploads):

```bash
tar czf ~/spotlight-backup-$(date +%F).tgz -C /opt/qubic_spotlight data
```

**Automatisches tägliches Backup** per Cron (3:30 Uhr nachts, 14 Tage Aufbewahrung):

```bash
mkdir -p ~/backups
( crontab -l 2>/dev/null; echo '30 3 * * * tar czf ~/backups/spotlight-$(date +\%F).tgz -C /opt/qubic_spotlight data && find ~/backups -name "spotlight-*.tgz" -mtime +14 -delete' ) | crontab -
```

> Für echte Ausfallsicherheit: Backups regelmäßig vom Server **wegkopieren**
> (z. B. per `scp` auf deinen PC oder in einen Object-Storage).

---

## 15. Sicherheits-Checkliste (zum Abhaken)

- [ ] Kein Root-Login per SSH (`PermitRootLogin no`)
- [ ] Kein Passwort-Login per SSH, nur SSH-Key (`PasswordAuthentication no`)
- [ ] Eigener sudo-User statt root im Alltag
- [ ] `ufw` aktiv, nur 22/80/443 offen
- [ ] App-Port 8080 **nicht** nach außen gemappt (nur via Caddy erreichbar)
- [ ] `fail2ban` läuft
- [ ] `unattended-upgrades` aktiv
- [ ] HTTPS erzwungen (Caddy + Let's Encrypt)
- [ ] CORS nur für öffentliche Endpunkte (`/api/ads*`, `/api/qubic/*`)
- [ ] Rate-Limiting aktiv (`/api/*`, 600/min pro IP)
- [ ] `.env` mit `chmod 600`, starkes `JWT_SECRET`
- [ ] Admin-Passwort nach erstem Login geändert
- [ ] Automatisches Backup eingerichtet + getestet

---

## 16. Troubleshooting

| Problem | Lösung |
|---------|--------|
| `JWT_SECRET fehlt in .env` | `.env` fehlt oder Variable leer. Im selben Ordner wie `docker-compose.yaml` prüfen. |
| Kein Zertifikat / HTTPS-Fehler | DNS prüfen (`dig +short spotlight.deine-domain.tld`). Port 80 muss offen sein. `docker compose logs caddy`. |
| App nicht erreichbar | `docker compose ps`, `docker compose logs -f qubic_spotlight`. „Now listening on" = ok. |
| Admin-Passwort vergessen | Container stoppen, `data/spotlight.db` löschen, neu starten → Admin aus `.env`. **Achtung: löscht alle Anzeigen.** |
| Aus SSH ausgesperrt | Über die Web-Konsole / Rescue des VPS-Anbieters einloggen und `sshd_config` korrigieren. |
| Logs | `docker compose logs -f` |
| Neustart | `docker compose restart` |
| Stoppen (Daten bleiben) | `docker compose down` |

---

*Stack-Referenz: .NET 10 Blazor Web App (WASM) · MudBlazor · LiteDB · REST/Swagger ·
JWT + API-Key. App-Port intern 8080, Daten unter `/data`, ausgehend zu `rpc.qubic.org`.*
