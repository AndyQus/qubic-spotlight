# Caddy mit Rate-Limit-Modul (nicht im Standard-Image enthalten).
# Wird vom docker-compose-Service "caddy" gebaut.
FROM caddy:2-builder AS builder
RUN xcaddy build --with github.com/mholt/caddy-ratelimit

FROM caddy:2
COPY --from=builder /usr/bin/caddy /usr/bin/caddy
