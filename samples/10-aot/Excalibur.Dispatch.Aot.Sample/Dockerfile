# Native AOT Dockerfile — AOT Sample (Worker)
# See: docs-site/docs/advanced/container-deployment.md §2.3
#
# Build:  docker build -t aot-sample .
# Run:    docker run --rm aot-sample
#
# Native AOT produces a self-contained binary with <100ms startup
# and minimal memory footprint. 150/170 packages are AOT-compatible.
# No .NET runtime required in the final image.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install AOT build prerequisites
RUN apt-get update && apt-get install -y --no-install-recommends \
    clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy project file and restore (layer caching)
COPY ["Excalibur.Dispatch.Aot.Sample.csproj", "."]
RUN dotnet restore

# Copy source and publish as AOT
COPY . .
RUN dotnet publish -c Release -o /app/publish \
    -r linux-x64 \
    -p:PublishAot=true

# Minimal runtime image — no .NET runtime needed
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# GC tuning for AOT workers (see container-deployment.md §5)
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_GCHeapHardLimit=0x8000000

# Run as non-root
USER app

ENTRYPOINT ["./Excalibur.Dispatch.Aot.Sample"]
