# Build context is the repository root (see docker-compose.yml / deploy compose files),
# because this project references ../../shared/Truckload.Contracts.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=1.0.0
WORKDIR /src

COPY shared/Truckload.Contracts/Truckload.Contracts.csproj shared/Truckload.Contracts/
COPY backend/Truckload.Ebs/Truckload.Ebs.csproj backend/Truckload.Ebs/
RUN dotnet restore backend/Truckload.Ebs/Truckload.Ebs.csproj

COPY shared/Truckload.Contracts/ shared/Truckload.Contracts/
COPY backend/Truckload.Ebs/ backend/Truckload.Ebs/
RUN dotnet publish backend/Truckload.Ebs/Truckload.Ebs.csproj -c Release -o /app -p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the docker-compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "Truckload.Ebs.dll"]
