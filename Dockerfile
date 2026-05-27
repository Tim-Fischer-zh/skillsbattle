# =============================================================================
# Killer Sudoku — App Image (Blazor Server, framework-dependent)
# =============================================================================
# Database is a separate container (mssql/server:2022-latest) wired via
# docker-compose.yml. This split was necessary because self-contained .NET 10
# publish drops the wwwroot/_framework Blazor JS bundle on linux/amd64 (.NET 10
# preview-stage quirk) — running app on the official aspnet:10.0 base image
# preserves the static-web-assets pipeline and ships blazor.web.js correctly.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY source/KillerSudoku.slnx ./
COPY source/src/KillerSudoku.Web/KillerSudoku.Web.csproj   src/KillerSudoku.Web/
COPY source/src/KillerSudoku.Core/KillerSudoku.Core.csproj src/KillerSudoku.Core/
COPY source/src/KillerSudoku.Data/KillerSudoku.Data.csproj src/KillerSudoku.Data/

COPY source/src/ src/
RUN dotnet publish src/KillerSudoku.Web/KillerSudoku.Web.csproj \
    -c Release -o /app/publish \
 && ls /app/publish/wwwroot/_framework/blazor.web.js

# ----------------------------------------------------------------------------
# Runtime: official ASP.NET Core 10 image with built-in static-web-assets
# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=4 \
    CMD curl -fsS http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "KillerSudoku.Web.dll"]
