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

# Build metadata, supplied by CI (docker/metadata-action + build-args); defaults keep local builds working.
ARG VERSION=1.0.0
ARG GIT_SHA=unknown
ARG BUILD_DATE=unknown
ARG SOURCE_URL=https://github.com/demortes/truckload-twitch-extension

# OCI standard labels.
LABEL org.opencontainers.image.title="truckload-ebs"       org.opencontainers.image.description="Truckload Twitch extension backend service (EBS)"       org.opencontainers.image.source="${SOURCE_URL}"       org.opencontainers.image.url="${SOURCE_URL}"       org.opencontainers.image.version="${VERSION}"       org.opencontainers.image.revision="${GIT_SHA}"       org.opencontainers.image.created="${BUILD_DATE}"       org.opencontainers.image.vendor="demortes"       org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:10.0"

# Datadog unified service tagging (service/env/version) picked up by the Agent from container
# labels, plus log source so the JSON stdout logs are parsed. `env` is deliberately not baked
# in: set it at runtime via the com.datadoghq.tags.env label / DD_ENV (see the compose files).
LABEL com.datadoghq.tags.service="truckload-ebs"       com.datadoghq.tags.version="${VERSION}"       com.datadoghq.ad.logs='[{"source":"csharp","service":"truckload-ebs"}]'

# Same tags as env vars for the process (and any Datadog tracer/SDK added later).
ENV DD_SERVICE=truckload-ebs     DD_VERSION=${VERSION}     DD_GIT_COMMIT_SHA=${GIT_SHA}     DD_GIT_REPOSITORY_URL=${SOURCE_URL}

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
