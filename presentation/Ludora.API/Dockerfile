# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["presentation/Ludora.API/Ludora.API.csproj", "presentation/Ludora.API/"]
COPY ["src/Core/Ludora.Application/Ludora.Application.csproj", "src/Core/Ludora.Application/"]
COPY ["src/Core/Ludora.Domain/Ludora.Domain.csproj", "src/Core/Ludora.Domain/"]
COPY ["src/Infrastructure/Ludora.Infrastructure/Ludora.Infrastructure.csproj", "src/Infrastructure/Ludora.Infrastructure/"]
COPY ["src/Infrastructure/Ludora.Persistence/Ludora.Persistence.csproj", "src/Infrastructure/Ludora.Persistence/"]
RUN dotnet restore "presentation/Ludora.API/Ludora.API.csproj"

COPY . .
RUN dotnet publish "presentation/Ludora.API/Ludora.API.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p \
      /data/wellplayd/public \
      /data/wellplayd/private \
      /data/wellplayd/data-protection-keys \
    && chown -R ${APP_UID}:${APP_UID} /data/wellplayd

COPY --from=build /app/publish .
USER ${APP_UID}
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ludora.API.dll"]
