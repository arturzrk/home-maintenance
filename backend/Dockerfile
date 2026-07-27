# ── Build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY HomeMaintenance.sln .
COPY src/HomeMaintenance.Domain/HomeMaintenance.Domain.csproj            src/HomeMaintenance.Domain/
COPY src/HomeMaintenance.Application/HomeMaintenance.Application.csproj  src/HomeMaintenance.Application/
COPY src/HomeMaintenance.Infrastructure/HomeMaintenance.Infrastructure.csproj src/HomeMaintenance.Infrastructure/
COPY src/HomeMaintenance.API/HomeMaintenance.API.csproj                  src/HomeMaintenance.API/

RUN dotnet restore src/HomeMaintenance.API/HomeMaintenance.API.csproj

COPY . .
RUN dotnet publish src/HomeMaintenance.API/HomeMaintenance.API.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

# curl is only for the HEALTHCHECK below; the aspnet base image has no
# HTTP client binary.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HomeMaintenance.API.dll"]
