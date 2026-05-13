# ── Stage 1: Build ────────────────────────────────────────────────────────
# Use the full .NET 8 SDK image to compile and publish the app.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy only the .csproj files first so Docker can cache the restore layer.
# If requirements don't change, this layer is reused even when code changes.
COPY API/API.csproj         API/
COPY Application/Application.csproj Application/
COPY Domain/Domain.csproj   Domain/
COPY Persistance/Persistance.csproj Persistance/

RUN dotnet restore API/API.csproj

# Now copy the full source and publish in Release mode.
COPY . .
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────
# Use the smaller ASP.NET runtime image (no SDK, ~200 MB smaller).
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy only the published artifacts from stage 1.
COPY --from=build /app/publish .

EXPOSE 5288

ENTRYPOINT ["dotnet", "API.dll"]



