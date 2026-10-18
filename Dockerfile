# syntax=docker/dockerfile:1

# ---- build: restore (cached layer) + publish ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/CatalogApi/CatalogApi.csproj src/CatalogApi/
RUN dotnet restore src/CatalogApi/CatalogApi.csproj
COPY src/ src/
RUN dotnet publish src/CatalogApi/CatalogApi.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- test (optional): docker build --target test . ----
FROM build AS test
COPY tests/ tests/
RUN dotnet test tests/CatalogApi.Tests/CatalogApi.Tests.csproj -c Release

# ---- runtime: small ASP.NET image, non-root ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
# $APP_UID (1654) is the built-in non-root user in .NET 8 images.
USER $APP_UID
ENTRYPOINT ["dotnet", "CatalogApi.dll"]
