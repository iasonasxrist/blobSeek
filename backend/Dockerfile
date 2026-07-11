FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGET_PROJECT=BlobSeek.Api
WORKDIR /src
COPY BlobSeek.sln ./
COPY Directory.Build.props Directory.Packages.props NuGet.config ./
COPY backend/src ./backend/src
RUN dotnet restore "backend/src/${TARGET_PROJECT}/${TARGET_PROJECT}.csproj"
RUN dotnet publish "backend/src/${TARGET_PROJECT}/${TARGET_PROJECT}.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
RUN adduser --disabled-password --gecos "" appuser && mkdir -p /data && chown -R appuser /data /app
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BlobSeek.Api.dll"]
