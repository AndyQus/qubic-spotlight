# Qubic Spotlight – Server-Setup (für den Docker-Admin)

Diese Anleitung beschreibt, wie Qubic Spotlight auf einem Ubuntu-Server mit Docker
betrieben wird. Der Quellcode wird **nicht** benötigt – das fertige Image liegt auf
Docker Hub. Du brauchst nur zwei Dateien: `docker-compose.yaml` und `.env`.

## Voraussetzungen

- Ubuntu-Server mit **Docker** und **Docker Compose** (`docker --version`,
  `docker compose version`).
- Ausgehender Internetzugriff (für `rpc.qubic.org` und Docker Hub).
- Ein Port nach außen (Standard 8080) bzw. ein Reverse Proxy davor (empfohlen, s. u.).

## 1. Verzeichnis anlegen

```bash
mkdir -p /opt/qubic_spotlight && cd /opt/qubic_spotlight
mkdir -p /root/spotlight/data        # persistente Daten (DB + Uploads)
```

## 2. docker-compose.yaml ablegen

Datei `/opt/qubic_spotlight/docker-compose.yaml`:

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
    ports:
      - "8080:8080"
    volumes:
      - /root/spotlight/data:/data
    restart: unless-stopped
```

## 3. .env anlegen (enthält die Geheimnisse, NICHT öffentlich)

Datei `/opt/qubic_spotlight/.env` im selben Ordner. Compose liest sie automatisch.

```ini
# Langes Zufallsgeheimnis (mind. 32 Zeichen) für die Signatur der Login-Tokens.
JWT_SECRET=HIER_EINSETZEN

# Initial-Admin – wird nur beim allerersten Start angelegt (solange DB leer ist).
ADMIN_EMAIL=admin@qubic.org
ADMIN_PASSWORD=HIER_EIN_TEMPORAERES_PASSWORT
```

`JWT_SECRET` bequem erzeugen:

```bash
echo "JWT_SECRET=$(openssl rand -base64 48)" >> .env
```

Rechte einschränken (nur root darf lesen):

```bash
chmod 600 .env
```

> Hinweis: `ADMIN_PASSWORD` wird nur beim **ersten** Start verwendet. Danach lebt der
> Account in der Datenbank; der Wert in `.env` ist dann bedeutungslos und kann geleert
> werden. Die App-Betreiber ändern das Passwort nach dem ersten Login in der App.

## 4. Starten

```bash
docker compose pull
docker compose up -d
docker compose logs -f        # Start beobachten; "Now listening on" = läuft
```

Aufrufbar dann unter `http://SERVER-IP:8080`. Login oben rechts mit `ADMIN_EMAIL` /
`ADMIN_PASSWORD`.

## 5. HTTPS / Reverse Proxy (empfohlen)

Die App spricht intern nur HTTP (Port 8080). Für eine öffentliche Domain einen
Reverse Proxy (Nginx, Caddy, Traefik) mit TLS davorsetzen, z. B. Caddy:

```
spotlight.qubic.org {
    reverse_proxy 127.0.0.1:8080
}
```

Wenn ein Proxy TLS terminiert, kann der Port 8080 in der Compose-Datei auch nur
lokal gebunden werden: `- "127.0.0.1:8080:8080"`.

## 6. Updates einspielen

```bash
cd /opt/qubic_spotlight
docker compose pull
docker compose up -d
```

Daten (Benutzer, Anzeigen, Bilder) bleiben erhalten – sie liegen im Volume unter
`/root/spotlight/data`, nicht im Container.

## 7. Backup

Es reicht, das Datenverzeichnis zu sichern:

```bash
tar czf spotlight-backup-$(date +%F).tgz /root/spotlight/data
```

## 8. Häufige Fragen / Troubleshooting

- **„JWT_SECRET fehlt in .env"** beim Start: Die `.env` fehlt oder die Variable ist
  leer. Datei prüfen (gleicher Ordner wie `docker-compose.yaml`).
- **Admin-Passwort vergessen / Login zurücksetzen**: Container stoppen, Datei
  `/root/spotlight/data/spotlight.db` löschen, neu starten – dann wird der Admin aus
  der `.env` neu angelegt. Achtung: löscht auch alle Anzeigen.
- **Logs ansehen**: `docker compose logs -f`
- **Neustart**: `docker compose restart`
- **Stoppen**: `docker compose down` (Daten bleiben im Volume erhalten).

## Was der Admin von uns braucht

- Diese Datei + die `docker-compose.yaml` (beides ist unkritisch / öffentlich).
- Den Wert für `ADMIN_PASSWORD` (temporär) – sicher übermittelt. `JWT_SECRET` kann der
  Admin selbst erzeugen.
