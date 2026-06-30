FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish qubic_spotlight/qubic_spotlight.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DATA_DIR=/data \
    LITEDB_FILE=spotlight.db

COPY --from=build /app/publish .

# Geo-Datenbank fest ins Image backen (db-ip Country Lite, CC-BY-4.0). Liegt
# getrennt von /data (Volume), damit die Länderermittlung auch ohne manuell
# bestücktes Volume funktioniert. GeoIpService sucht hier als Fallback.
COPY --from=build /src/qubic_spotlight/GeoData/dbip-country-lite.mmdb /app/GeoData/dbip-country-lite.mmdb

EXPOSE 8080
ENTRYPOINT ["dotnet", "qubic_spotlight.dll"]
