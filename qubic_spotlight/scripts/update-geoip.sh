#!/usr/bin/env bash
# Lädt die kostenlose, account-freie "IP to Country Lite"-DB von db-ip.com herunter
# und legt sie als dbip-country-lite.mmdb im Daten-Verzeichnis ab.
#
# Lizenz: CC-BY-4.0 — Attribution "IP Geolocation by DB-IP" ist Pflicht (im Footer
# der App bereits enthalten). Es findet kein Account/Login statt.
#
# Verwendung (lokal):   ./update-geoip.sh ./Data
# Verwendung (Server):  ./update-geoip.sh /opt/qubic_spotlight/data
# Ideal als monatlicher Cron-Job (die DB wird monatlich aktualisiert).
set -euo pipefail

DEST_DIR="${1:-${DATA_DIR:-./Data}}"
mkdir -p "$DEST_DIR"
OUT="$DEST_DIR/dbip-country-lite.mmdb"

# db-ip veröffentlicht monatlich unter dbip-country-lite-YYYY-MM.mmdb.gz.
# Aktuellen Monat versuchen, sonst auf den Vormonat zurückfallen.
try_download() {
    local ym="$1"
    local url="https://download.db-ip.com/free/dbip-country-lite-${ym}.mmdb.gz"
    echo "Versuche: $url"
    if curl -fsSL "$url" -o "$OUT.gz"; then
        gunzip -f "$OUT.gz"
        echo "OK -> $OUT"
        return 0
    fi
    return 1
}

CUR="$(date -u +%Y-%m)"
PREV="$(date -u -d 'last month' +%Y-%m 2>/dev/null || date -u -v-1m +%Y-%m)"

if try_download "$CUR" || try_download "$PREV"; then
    echo "Fertig. Datenbank aktualisiert."
    echo "Hinweis: App neu starten bzw. Container 'qubic_spotlight' restarten, damit die DB neu geladen wird."
else
    echo "FEHLER: Download fehlgeschlagen (weder $CUR noch $PREV verfügbar)." >&2
    exit 1
fi
