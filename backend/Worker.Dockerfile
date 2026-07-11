FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BlobSeek.sln ./
COPY Directory.Build.props Directory.Packages.props NuGet.config ./
COPY backend/src ./backend/src
RUN dotnet restore backend/src/BlobSeek.Worker/BlobSeek.Worker.csproj
RUN dotnet publish backend/src/BlobSeek.Worker/BlobSeek.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser && mkdir -p /data && chown -R appuser /data /app
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BlobSeek.Worker.dll"]
