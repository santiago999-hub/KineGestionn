# syntax=docker/dockerfile:1

# ─── BUILD ─────────────────────────────────────────────────────────────────────
# Publica KineGestion.Web en Release. Solo se copian los csproj necesarios
# (Web referencia a Core y Data) para aprovechar el cache de capas de NuGet.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["KineGestion.Web/KineGestion.Web.csproj", "KineGestion.Web/"]
COPY ["KineGestion.Core/KineGestion.Core.csproj", "KineGestion.Core/"]
COPY ["KineGestion.Data/KineGestion.Data.csproj", "KineGestion.Data/"]
RUN dotnet restore KineGestion.Web/KineGestion.Web.csproj

COPY . .
RUN dotnet publish KineGestion.Web/KineGestion.Web.csproj -c Release --no-restore -o /app/publish

# ─── RUNTIME ───────────────────────────────────────────────────────────────────
# Imagen ASP.NET 8 sin SDK. Se ejecuta como usuario sin privilegios (APP_UID).
# /app/keyring pertenece a ese usuario: ahí persisten las claves DataProtection
# de cookies/antiforgery (montar un volumen para sobrevivir reinicios).
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

USER root
RUN mkdir -p /app/keyring && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DataProtection__KeyRingPath=/app/keyring
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "KineGestion.Web.dll"]