# syntax=docker/dockerfile:1.4
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies with NuGet token
COPY ESPNScrape.csproj .
RUN --mount=type=secret,id=nuget_token \
    dotnet nuget add source "https://nuget.pkg.github.com/BoomBustFantasy/index.json" \
    --name github \
    --username "BoomBustFantasy" \
    --password "$(cat /run/secrets/nuget_token)" \
    --store-password-in-clear-text && \
    dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage - use aspnet for web apps
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published output
COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Create a non-root user for security
RUN adduser --disabled-password --home /app --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "ESPNScrape.dll"]
