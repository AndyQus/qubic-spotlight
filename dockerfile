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
EXPOSE 8080
ENTRYPOINT ["dotnet", "qubic_spotlight.dll"]
