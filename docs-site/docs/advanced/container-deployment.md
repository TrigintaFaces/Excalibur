---
sidebar_position: 8
title: Container Deployment Guide
description: Deploy Excalibur.Dispatch applications in Docker, Kubernetes, and Azure Container Apps with AOT, health probes, GC tuning, and graceful shutdown.
---

# Container Deployment Guide

Deploy Excalibur.Dispatch applications in containers with production-ready health probes, GC tuning, graceful shutdown, and Native AOT support.

This guide focuses specifically on container-optimized deployment. For non-container scenarios (IIS, Windows Service, Azure Functions), see [Deployment](../deployment/index.md). For AOT source generator setup, see [Native AOT](./native-aot.md).

---

## 1. Choosing a Build Strategy

Pick the strategy that matches your workload:

| Strategy | Best For | Startup | Memory | Dispatch Compatibility |
|----------|----------|---------|--------|----------------------|
| **JIT** | Long-running APIs, plugin architectures | ~600 ms | Higher | All 170 packages |
| **ReadyToRun** | APIs needing fast startup + full features | ~380 ms | Medium | All 170 packages |
| **Native AOT** | Workers, jobs, event handlers, sidecars | &lt;100 ms | Lowest | 150/170 packages |

150 of 170 packages are AOT-compatible. Core dispatch, pipeline, handlers, and most transports work fully in AOT. Packages depending on external SDKs without AOT support (Kafka/Confluent, AWS SDK) remain JIT-only -- see the [AOT Compatibility Matrix](./aot-compatibility.md).

:::tip AOT deserialization trade-off
AOT eliminates JIT warmup, giving faster startup. However, source-generated deserialization can be 41-93% slower for complex message types compared to JIT-optimized paths (measured in `AotPathSerializationBenchmarks`). For most container workloads, the startup improvement outweighs the per-message overhead. Profile your specific workload to decide.
:::

---

## 2. Dockerfile Recipes

Three production-ready Dockerfiles. All examples use .NET 10. For .NET 8 or 9, replace image tags accordingly (e.g., `sdk:10.0` to `sdk:9.0`).

### 2.1 JIT (Default)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/MyApp/MyApp.csproj", "src/MyApp/"]
RUN dotnet restore
COPY . .
RUN dotnet publish src/MyApp -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

Standard multi-stage build. `USER app` runs as non-root. Compatible with all packages.

### 2.2 ReadyToRun

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/MyApp/MyApp.csproj", "src/MyApp/"]
RUN dotnet restore
COPY . .
RUN dotnet publish src/MyApp -c Release -o /app/publish \
    -p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

20-40% faster startup. Images are 20-60% larger. Architecture-specific (`linux-x64`).

### 2.3 Native AOT

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/MyApp/MyApp.csproj", "src/MyApp/"]
RUN dotnet restore
COPY . .
RUN dotnet publish src/MyApp -c Release -o /app/publish \
    -r linux-x64 -p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["./MyApp"]
```

Chiseled base image (~10 MB). No .NET runtime needed. Requires `AddGeneratedServices()` in `Program.cs`. Expect zero IL2xxx/IL3xxx warnings for supported packages.

---

## 3. Health Checks and Kubernetes Probes

### 3.1 Framework Health Check Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExcalibur(excalibur => { /* configure dispatch */ });

// Option A: Unified registration with transport health
builder.Services.AddExcaliburHealthChecks(withHealthChecks: checks =>
    checks.AddTransportHealthChecks());

// Option B: Separate registration
builder.Services.AddHealthChecks()
    .AddTransportHealthChecks(); // Tags: "transports"
builder.Services.AddExcaliburHealthChecks();

var app = builder.Build();
app.Run();
```

`AddExcaliburHealthChecks()` maps the readiness endpoint to `/.well-known/ready` by default. Customize via the `endpointUri` parameter.

### 3.2 Kubernetes Probe Mapping

```yaml
startupProbe:
  httpGet:
    path: /.well-known/ready
    port: 8080
  periodSeconds: 3
  failureThreshold: 20        # 60s max startup window
  # Allows transport connections to establish before
  # readiness probe takes over

readinessProbe:
  httpGet:
    path: /.well-known/ready
    port: 8080
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 3

livenessProbe:
  httpGet:
    path: /healthz/live         # Dedicated liveness -- NO dependency checks
    port: 8080
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
```

:::warning Liveness vs Readiness separation
The liveness probe must NOT check transport or database dependencies. If a broker goes down temporarily and liveness fails, Kubernetes restarts the pod -- making the outage worse. Use a dedicated `/healthz/live` endpoint that returns 200 if the process is running. Use `/.well-known/ready` (with transport health) only for readiness.
:::

**Liveness endpoint setup:**

```csharp
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false // No dependency checks -- just "process is alive"
});
```

**Key guidance:**
- Startup probe prevents Kubernetes from killing the pod while transports initialize
- `MultiTransportHealthCheck` reports Unhealthy when transports are not running
- `MultiTransportHealthCheck` also reports Unhealthy during transport initialization (before `StartAsync` completes), which is the correct behavior for startup probes
- For AOT apps, startup is fast (under 100 ms) but transport connections still take time
- Adjust `failureThreshold` based on your broker's connection time

### 3.3 ThrowOnStartupFailure

`TransportAdapterHostedServiceOptions.ThrowOnStartupFailure` defaults to `true`. If a transport cannot connect at startup, the app crashes. In Kubernetes, this triggers CrashLoopBackOff with exponential backoff -- the standard pattern for "wait until dependency is ready."

Do not set `ThrowOnStartupFailure = false` unless you have a specific degraded-operation scenario where the app should continue without its transport.

---

## 4. Graceful Shutdown and Drain Alignment

```yaml
spec:
  terminationGracePeriodSeconds: 35  # >= DrainTimeoutSeconds + 5
  containers:
  - name: api
    # ...
```

**Rule:** `terminationGracePeriodSeconds` must be >= `DrainTimeoutSeconds` (default: 30) plus a small buffer (5s) for SIGTERM propagation.

**What happens on shutdown:**

1. Kubernetes sends SIGTERM -- .NET host triggers `ApplicationStopping`
2. `TransportAdapterHostedService.StopAsync` begins drain (reverse start order)
3. Each adapter gets up to `DrainTimeoutSeconds` to finish in-flight messages
4. If drain exceeds timeout, adapter is forcefully stopped (logged as warning)
5. After `terminationGracePeriodSeconds`, Kubernetes sends SIGKILL

**Configuration:**

```csharp
builder.Services.Configure<TransportAdapterHostedServiceOptions>(options =>
{
    options.DrainTimeoutSeconds = 30; // Default
});
```

---

## 5. GC Tuning Recipes

:::info Starting points, not prescriptions
GC behavior is highly workload-dependent -- allocation rate, object lifetime distribution, and message size all affect optimal settings. Use these profiles as baselines, then profile your specific workload with `dotnet-counters` and adjust.
:::

### 5.1 API Service (512 MiB limit)

```yaml
env:
- name: DOTNET_gcServer
  value: "1"
- name: DOTNET_GCHeapHardLimitPercent
  value: "65"
resources:
  requests:
    memory: "384Mi"
    cpu: "500m"
  limits:
    memory: "512Mi"
    cpu: "1000m"
```

Server GC with 65% heap cap. Leaves headroom for native allocations, thread stacks, and runtime overhead.

### 5.2 Background Worker (256 MiB limit)

```yaml
env:
- name: DOTNET_gcServer
  value: "0"        # Workstation GC -- lower overhead for single-core
- name: DOTNET_GCHeapHardLimitPercent
  value: "60"
resources:
  requests:
    memory: "192Mi"
    cpu: "250m"
  limits:
    memory: "256Mi"
    cpu: "500m"
```

Workstation GC for workers processing queue messages. Lower memory overhead, good for scale-to-zero scenarios.

### 5.3 High-Throughput gRPC Service (1 GiB limit)

```yaml
env:
- name: DOTNET_gcServer
  value: "1"
- name: DOTNET_GCHeapHardLimitPercent
  value: "75"
resources:
  requests:
    memory: "768Mi"
    cpu: "2000m"
  limits:
    memory: "1Gi"
    cpu: "2000m"
```

Server GC with higher heap allowance for sustained throughput. Pin CPU requests = limits to avoid throttling.

### 5.4 cgroup v2 Note

AKS and Azure Linux node pools now default to cgroup v2. The .NET runtime correctly reads v2 memory limits, but RSS metrics from `kubectl top pod` may differ from v1 by 5-15% due to different page cache accounting. If you see unexpected OOMs after a cluster upgrade, revalidate your `DOTNET_GCHeapHardLimitPercent` settings -- the runtime now perceives less available memory under v2.

---

## 6. Running with Sidecars

Each sidecar process reads the pod's memory limit and allocates for its own use. Combined, they can exceed the limit and trigger OOMKill.

**Fix:** Split limits explicitly per container:

```yaml
containers:
- name: api
  image: myapi:aot
  resources:
    limits:
      memory: "384Mi"
  env:
  - name: DOTNET_GCHeapHardLimitPercent
    value: "65"    # 65% of 384 MiB = ~250 MiB managed heap
- name: dapr
  image: daprio/daprd:1.13.2
  resources:
    limits:
      memory: "128Mi"
```

**When to use sidecars vs in-process:**

| Concern | Sidecar | In-Process |
|---------|---------|------------|
| mTLS between services | Envoy/Istio | N/A |
| Retry/circuit breaker | Dapr (if polyglot) | Polly (recommended for .NET-only) |
| Observability | OTEL Collector | Direct OTLP export (leaner) |
| Pub/sub | Dapr bindings | Direct transport SDK (lower latency) |

For .NET-only deployments using Excalibur.Dispatch transports, in-process is almost always leaner. Dispatch already handles retry, transport abstraction, and observability.

---

## 7. Azure Container Apps

### 7.1 Cold-Start Optimization

- Use Native AOT for event-triggered workloads (queue, timer)
- Keep images under 100 MB (achievable with AOT + chiseled base)
- Set `minReplicas: 1` for latency-sensitive APIs to avoid scale-from-zero

### 7.2 KEDA Scaling for Queue Workers

```yaml
scale:
  minReplicas: 0
  maxReplicas: 10
  rules:
  - name: queue-trigger
    azureQueue:
      queueName: dispatch-messages
      queueLength: 5
```

AOT workers start in under 100 ms, making scale-from-zero practical for bursty workloads.

---

## 8. Observability in Containers

Excalibur.Dispatch includes built-in OpenTelemetry instrumentation. In containers, add these container-specific considerations:

**Live GC diagnostics:**

```bash
# Inside a running pod
kubectl exec -it <pod> -- dotnet-counters monitor \
    --counters System.Runtime,Microsoft.Extensions.Hosting
```

**Key metrics to monitor:**
- `gc-heap-size-bytes` -- managed heap usage against your `GCHeapHardLimitPercent`
- `threadpool-queue-length` -- saturation indicator for dispatch throughput
- Transport health status via `/.well-known/ready`

**OpenTelemetry export:**

Excalibur.Dispatch's `ActivitySource` and `Meter` instrumentation (prefixed `Excalibur.Dispatch.*`) integrates with the standard `OpenTelemetry.Extensions.Hosting` pipeline. Export traces and metrics directly via OTLP (leaner than a sidecar collector for .NET-only deployments):

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Excalibur.Dispatch.*")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("Excalibur.Dispatch.*")
        .AddOtlpExporter());
```

For detailed observability setup, see [Observability](../observability/index.md).

---

## Quick Reference

| Setting | Default | Where |
|---------|---------|-------|
| Health endpoint | `/.well-known/ready` | `AddExcaliburHealthChecks()` |
| Drain timeout | 30 seconds | `TransportAdapterHostedServiceOptions.DrainTimeoutSeconds` |
| Throw on startup failure | `true` | `TransportAdapterHostedServiceOptions.ThrowOnStartupFailure` |
| AOT packages | 150/170 | [Compatibility Matrix](./aot-compatibility.md) |
