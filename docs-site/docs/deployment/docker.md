# Docker Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** Docker containers
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Docker Desktop or Docker Engine installed
- Familiarity with [ASP.NET Core deployment](./aspnet-core.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Overview

This guide covers Docker containerization for Excalibur applications, including multi-stage builds, production optimizations, and container orchestration patterns.

**Use Docker when:**
- Deploying to cloud platforms (Azure Container Instances, AWS ECS, Google Cloud Run)
- Running in Kubernetes clusters
- Ensuring consistent environments across dev, staging, and production
- Simplifying dependency management

---

## Quick Start

### Minimal Dockerfile

```dockerfile
# Minimal ASP.NET Core application with Excalibur.Dispatch
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY publish/ ./
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### Build and Run

```bash
# Publish application
dotnet publish -c Release -o publish

# Build Docker image
docker build -t your-app:latest .

# Run container
docker run -p 8080:8080 your-app:latest
```

---

## Production-Ready Dockerfile

### Multi-Stage Build

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy project files
COPY ["src/YourApp/YourApp.csproj", "src/YourApp/"]
COPY ["src/YourApp.Domain/YourApp.Domain.csproj", "src/YourApp.Domain/"]

# Restore dependencies
RUN dotnet restore "src/YourApp/YourApp.csproj"

# Copy source code
COPY . .

# Build application
WORKDIR "/src/src/YourApp"
RUN dotnet build "YourApp.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "YourApp.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -g 1000 appuser && \
    adduser -u 1000 -G appuser -s /bin/sh -D appuser

# Copy published files
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Entry point
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

**Benefits:**
- ✅ Multi-stage build minimizes final image size (~100MB vs ~1GB)
- ✅ Alpine Linux base reduces attack surface
- ✅ Non-root user improves security
- ✅ Health check enables container orchestration
- ✅ Layer caching optimizes build time

---

## Environment-Specific Configurations

### Development (docker-compose.yml)

```yaml
services:
  app:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__Default=Server=db;Database=AppDb;User=sa;Password=YourStrong!Passw0rd
    depends_on:
      db:
        condition: service_healthy
    networks:
      - app-network

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong!Passw0rd
    ports:
      - "1433:1433"
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -Q 'SELECT 1' -C || exit 1"]
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 10s
    networks:
      - app-network
    volumes:
      - sqldata:/var/opt/mssql

networks:
  app-network:
    driver: bridge

volumes:
  sqldata:
```

**Usage:**

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f app

# Stop all services
docker-compose down

# Clean up volumes
docker-compose down -v
```

---

### Production (Azure Container Registry)

```dockerfile
# Production Dockerfile with optimizations
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy and restore (cached layer)
COPY ["*.sln", "./"]
COPY ["src/**/*.csproj", "src/"]
RUN for file in $(find src -name "*.csproj"); do \
      mkdir -p $(dirname $file) && \
      mv $(basename $file) $file; \
    done
RUN dotnet restore

# Copy source and build
COPY . .
RUN dotnet publish "src/YourApp/YourApp.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

# Non-root user
RUN addgroup -g 1000 appuser && \
    adduser -u 1000 -G appuser -s /bin/sh -D appuser

# Copy application
COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "YourApp.dll"]
```

**Deployment:**

```bash
# Build image
docker build -t yourregistry.azurecr.io/your-app:v1.0.0 .

# Push to registry
docker push yourregistry.azurecr.io/your-app:v1.0.0

# Deploy to Azure Container Instances
az container create \
  --resource-group your-rg \
  --name your-app \
  --image yourregistry.azurecr.io/your-app:v1.0.0 \
  --dns-name-label your-app \
  --ports 8080
```

---

## Excalibur Configuration

### Outbox Pattern with Background Service

```dockerfile
# Enable background services in container
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app

# ... (previous steps)

# Required for background services
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Configure outbox processor interval
ENV Dispatch__Outbox__ProcessorInterval=00:00:30
ENV Dispatch__Outbox__BatchSize=100

ENTRYPOINT ["dotnet", "YourApp.dll"]
```

**Program.cs configuration:**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure outbox with SQL Server storage and background processing
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(builder.Configuration.GetConnectionString("Default")!)
          .WithProcessing(p => p.BatchSize(100)
                                .PollingInterval(TimeSpan.FromSeconds(30)))
          .EnableBackgroundProcessing();
});

var app = builder.Build();
app.Run();
```

---

### Event Sourcing with Projections

```dockerfile
# Multi-container setup for event sourcing
# docker-compose.yml
services:
  write-api:
    build:
      context: .
      dockerfile: Dockerfile.WriteApi
    environment:
      - EventStore__ConnectionString=Server=eventstore;Database=Events;...
    depends_on:
      - eventstore
    networks:
      - app-network

  projections:
    build:
      context: .
      dockerfile: Dockerfile.Projections
    environment:
      - EventStore__ConnectionString=Server=eventstore;Database=Events;...
      - ReadStore__ConnectionString=Server=readstore;Database=Projections;...
    depends_on:
      - eventstore
      - readstore
    networks:
      - app-network

  read-api:
    build:
      context: .
      dockerfile: Dockerfile.ReadApi
    environment:
      - ReadStore__ConnectionString=Server=readstore;Database=Projections;...
    depends_on:
      - readstore
    networks:
      - app-network

  eventstore:
    image: mcr.microsoft.com/mssql/server:2022-latest
    # ... (SQL Server configuration)

  readstore:
    image: mcr.microsoft.com/mssql/server:2022-latest
    # ... (SQL Server configuration)

networks:
  app-network:
```

---

## Security Best Practices

### 1. Use Non-Root Users

```dockerfile
# GOOD: Non-root user
RUN addgroup -g 1000 appuser && \
    adduser -u 1000 -G appuser -s /bin/sh -D appuser
USER appuser

# BAD: Running as root
# (no USER directive = runs as root)
```

### 2. Scan for Vulnerabilities

```bash
# Using Docker Scout
docker scout cves your-app:latest

# Using Trivy
trivy image your-app:latest
```

### 3. Use Minimal Base Images

```dockerfile
# GOOD: Alpine (smaller attack surface)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine

# ACCEPTABLE: Debian Slim
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim

# AVOID: Full Debian (larger attack surface)
# FROM mcr.microsoft.com/dotnet/aspnet:9.0
```

### 4. Secrets Management

```bash
# GOOD: Use Docker secrets or environment variables
docker run -e ConnectionStrings__Default="$CONNECTION_STRING" your-app

# BAD: Hardcoding secrets in Dockerfile
# ENV ConnectionStrings__Default="Server=..."
```

**Better: Use Azure Key Vault or AWS Secrets Manager**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    // Load secrets from Azure Key Vault
    builder.Configuration.AddAzureKeyVault(
        new Uri($"https://{vaultName}.vault.azure.net/"),
        new DefaultAzureCredential());
}
```

---

## Optimizations

### Build Cache Optimization

```dockerfile
# GOOD: Copy only project files first (cached layer)
COPY ["src/YourApp/YourApp.csproj", "src/YourApp/"]
RUN dotnet restore

# Then copy source (invalidates cache only when code changes)
COPY . .
RUN dotnet build

# BAD: Copy everything at once (cache invalidated on any change)
# COPY . .
# RUN dotnet restore && dotnet build
```

### Multi-Platform Builds

```bash
# Build for multiple architectures
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t your-app:latest \
  --push \
  .
```

### Image Size Reduction

```dockerfile
# Use trimming (reduces size by ~30%)
RUN dotnet publish \
    -c Release \
    -o /app/publish \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial
```

**Before:** 120MB
**After:** 85MB

---

## Health Checks

### ASP.NET Core Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddCheck<OutboxHealthCheck>("outbox");

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

### Dockerfile Health Check

```dockerfile
# HTTP health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# For images without curl
HEALTHCHECK --interval=30s CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
```

---

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker logs <container-id>

# Interactive shell
docker run -it --entrypoint /bin/sh your-app:latest

# Check environment variables
docker inspect <container-id> | grep Env -A 20
```

### High Memory Usage

```bash
# Limit memory
docker run -m 512m your-app:latest

# Monitor resource usage
docker stats <container-id>
```

### Connection Issues

```bash
# Check network
docker network inspect bridge

# Test connectivity
docker run --rm --network container:<container-id> alpine ping -c 3 db
```

---

## CI/CD Integration

### GitHub Actions

```yaml
name: Build and Push Docker Image

on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Login to Azure Container Registry
        uses: docker/login-action@v3
        with:
          registry: yourregistry.azurecr.io
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}

      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: yourregistry.azurecr.io/your-app:${{ github.sha }}
          cache-from: type=registry,ref=yourregistry.azurecr.io/your-app:latest
          cache-to: type=inline
```

---

## Next Steps

- **Kubernetes:** [Deploy to Kubernetes](kubernetes.md) for production orchestration
- **Azure:** [Azure Functions](azure-functions.md) for serverless deployment
- **Security:** [Security Best Practices](security-best-practices.md) for hardening
- **Monitoring:** [Health Checks](../observability/health-checks.md) for observability

---

## See Also

- [Kubernetes Deployment](kubernetes.md) - Orchestrate Docker containers in production with Kubernetes
- [On-Premises Deployment](on-premises.md) - Deploy to on-premises servers using IIS, Windows Services, or systemd
- [Health Checks](../observability/health-checks.md) - Configure health check endpoints for container orchestration

---

**Last Updated:** 2026-01-01
**Framework:** Excalibur 1.0.0
**Target:** Docker 20.10+, .NET 9.0
