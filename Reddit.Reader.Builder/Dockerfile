# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Reddit.Reader.Builder.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create directories that the pipeline writes to at runtime.
# Mount these as volumes to persist output across container restarts.
RUN mkdir -p /app/output

COPY --from=build /app/publish .

# Pipeline runtime config overrides — secrets should be supplied via
# environment variables or a mounted appsettings.Production.json.
ENV DOTNET_ENVIRONMENT=Production \
    Pipeline__OutputDir=/app/output \
    Pipeline__SeenIdsFile=/app/output/seen_ids.json \
    Pipeline__FeedFile=/app/output/feed-0001.xml

VOLUME ["/app/output"]

ENTRYPOINT ["dotnet", "Reddit.Reader.Builder.dll"]
