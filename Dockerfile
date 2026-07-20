# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the central manifests first for better layer caching.
COPY global.json Directory.Build.props Directory.Packages.props OfrenCollect.slnx ./
COPY src/ ./src/
RUN dotnet restore src/OfrenCollect.Api/OfrenCollect.Api.csproj

RUN dotnet publish src/OfrenCollect.Api/OfrenCollect.Api.csproj \
    -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production
# The host (Render/Koyeb) injects PORT; Program.cs binds to it. 8080 is the local default.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OfrenCollect.Api.dll"]
