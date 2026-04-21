# ReadyToRun Dockerfile — RabbitMQ Transport Sample
# See: docs-site/docs/advanced/container-deployment.md §2.2
#
# Build:  docker build -t rabbitmq-sample .
# Run:    docker compose up   (starts RabbitMQ + app)
#
# ReadyToRun provides 20-40% faster startup while maintaining
# full package compatibility (all 170 packages supported).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore (layer caching)
COPY ["RabbitMQSample.csproj", "."]
RUN dotnet restore

# Copy source and publish with ReadyToRun
COPY . .
RUN dotnet publish -c Release -o /app/publish \
    -p:PublishReadyToRun=true

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# GC tuning for container workloads (see container-deployment.md §5)
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_GCHeapHardLimit=0x10000000

# Run as non-root
USER app

# Health probe port
EXPOSE 8080

ENTRYPOINT ["dotnet", "RabbitMQSample.dll"]
