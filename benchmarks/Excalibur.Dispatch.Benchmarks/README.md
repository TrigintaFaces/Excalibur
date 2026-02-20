# Excalibur Performance Benchmarks

Comprehensive BenchmarkDotNet suite for **Excalibur** framework performance validation and competitive analysis.

`.NET 10.0` | `317 benchmarks in latest full diagnostics matrix`

---

## Table of Contents

1. [Overview](#overview)
2. [Quick Start](#quick-start)
3. [Benchmark Categories](#benchmark-categories)
4. [Diagnostic Slowdown Isolation](#7-diagnostic-slowdown-isolation-benchmarks)
5. [Running Benchmarks](#running-benchmarks)
6. [Performance Targets](#performance-targets)
7. [Interpreting Results](#interpreting-results)
8. [Comparative Analysis](#comparative-analysis)
9. [Infrastructure](#infrastructure)
10. [Contributing](#contributing)

---

## Overview

This benchmark suite provides:

- ✅ **Core Framework Benchmarks** - Event Sourcing, Outbox, Pipeline, Serialization
- ✅ **Competitive Analysis** - Direct comparison with MediatR and Wolverine
- ✅ **Performance Baselines** - Regression detection and continuous monitoring
- ✅ **Real-World Scenarios** - SQL Server, concurrency, batching, large payloads
- ✅ **Memory Profiling** - Gen0/Gen1 collections, allocations, GC pressure

**Target Runtime**: .NET 10.0

**Infrastructure**: BenchmarkDotNet + Testcontainers (SQL Server 2022)

---

## Quick Start

### Run All Benchmarks

```bash
# From repository root
cd benchmarks/Excalibur.Dispatch.Benchmarks
dotnet run -c Release
```

### Run Comparative + Diagnostics Matrix (Recommended for Doc Sync)

```bash
# From repository root
pwsh ./eng/run-benchmark-matrix.ps1
```

This runs the comparative + diagnostics class matrix sequentially, writes per-class logs to
`BenchmarkDotNet.Artifacts/results/run-<Class>-<timestamp>.log`, and emits a matrix summary:

- `BenchmarkDotNet.Artifacts/results/benchmark-matrix-summary-<timestamp>.json`
- `BenchmarkDotNet.Artifacts/results/benchmark-matrix-summary-<timestamp>.md`

Useful flags:

```bash
# Comparative-only classes
pwsh ./eng/run-benchmark-matrix.ps1 -ComparativeOnly

# Diagnostics-only classes
pwsh ./eng/run-benchmark-matrix.ps1 -DiagnosticsOnly

# Quick smoke subset
pwsh ./eng/run-benchmark-matrix.ps1 -CiSmoke -NoBuild -NoRestore

# Focused parity rerun (MediatR + routing-first only, per-class logs)
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes MediatRComparisonBenchmarks,RoutingFirstParityBenchmarks,WolverineComparisonBenchmarks,MassTransitComparisonBenchmarks -ArtifactsPath ./BenchmarkDotNet.Artifacts.FullRefresh-20260219

# Keep verbose framework logs (default is quiet warning-level logs)
pwsh ./eng/run-benchmark-matrix.ps1 -VerboseFrameworkLogs
```

### Run Specific Category

```bash
# Event Sourcing benchmarks only
dotnet run -c Release --filter *EventAppend*

# Comparative benchmarks (MediatR + Wolverine)
dotnet run -c Release --filter *Comparison*

# Outbox pattern benchmarks
dotnet run -c Release --filter *Outbox*

# Diagnostic slowdown isolation suites
dotnet run -c Release --filter *DispatchHotPathBreakdownBenchmarks*
dotnet run -c Release --filter *MiddlewareCostCurveBenchmarks*
dotnet run -c Release --filter *HandlerResolutionBenchmarks*
dotnet run -c Release --filter *ActivationStrategyBenchmarks*
dotnet run -c Release --filter *HandlerFanOutBenchmarks*
dotnet run -c Release --filter *FanOutColdDecompositionBenchmarks*
# Note: FanOutColdDecompositionBenchmarks uses a dedicated cold-start config (`diag-cold`) for lower-noise cold-path measurements.
dotnet run -c Release --filter *FanOutBehaviorMatrixBenchmarks*
dotnet run -c Release --filter *TransportAdapterPhaseBenchmarks*
dotnet run -c Release --filter *TransportConcurrencyBreakdownBenchmarks*
dotnet run -c Release --filter *FailurePathBenchmarks*
dotnet run -c Release --filter *RetryPolicyMicroBenchmarks*
dotnet run -c Release --filter *CancellationCostBenchmarks*
dotnet run -c Release --filter *DispatchContextCostBenchmarks*
dotnet run -c Release --filter *AllocationHotspotBenchmarks*
dotnet run -c Release --filter *ConcurrencyContentionBenchmarks*
dotnet run -c Release --filter *LongRunAllocationGcBenchmarks*
dotnet run -c Release --filter *HandlerInvokerPathBenchmarks*
```

### View Results

Results are automatically exported to `BenchmarkDotNet.Artifacts/results/`:

- **HTML**: Interactive charts and tables
- **CSV**: Machine-readable data
- **Markdown**: GitHub-friendly summaries

---

## Benchmark Categories

### 1. Baseline Benchmarks

**File**: `BaselineBenchmarks.cs`

Simple infrastructure validation benchmarks to verify BenchmarkDotNet setup.

| Benchmark | Description |
|-----------|-------------|
| `StringConcatenation` | Baseline string operations |
| `StringInterpolation` | String interpolation performance |
| `StringFormat` | String.Format performance |

**Purpose**: Sanity check for benchmark infrastructure (MemoryDiagnoser, exporters, statistical analysis).

---

### 2. Event Sourcing Benchmarks

**Directory**: `Patterns/`

Comprehensive benchmarks for event store operations using SQL Server.

#### EventAppendBenchmarks.cs

| Benchmark | Payload Size | Target (P50/P95) |
|-----------|--------------|------------------|
| `AppendSingleEvent` | 1KB | < 10ms / < 20ms |
| `AppendTenEvents` | 10KB | < 50ms / < 100ms |
| `AppendHundredEvents` | 100KB | < 100ms / < 200ms |
| `AppendThousandEvents` | 1MB | < 500ms / < 1000ms |
| `AppendConcurrentTenAggregates` | 10KB (concurrent) | < 100ms / < 200ms |

#### AggregateLoadBenchmarks.cs

| Benchmark | Event Count | Target (P50/P95) |
|-----------|-------------|------------------|
| `LoadAggregateSmall` | 10 events | < 50ms / < 100ms |
| `LoadAggregateMedium` | 100 events | < 100ms / < 200ms |
| `LoadAggregateLarge` | 1000 events | < 500ms / < 1000ms |
| `LoadAggregateConcurrentTen` | 10 events (concurrent) | < 100ms / < 200ms |

**Infrastructure**: SQL Server 2022 (Testcontainers), SqlServerEventStore

**Key Metrics**: Append latency, load latency, memory allocations, GC collections

---

### 3. Outbox Pattern Benchmarks

**Directory**: `Patterns/`

Transactional outbox pattern performance for reliable message delivery.

#### OutboxStagingBenchmarks.cs

| Benchmark | Messages | Target (P50/P95) |
|-----------|----------|------------------|
| `StageSingleMessage` | 1 | < 5ms / < 10ms |
| `StageTenMessages` | 10 | < 20ms / < 40ms |
| `StageHundredMessages` | 100 | < 50ms / < 100ms |
| `StageThousandMessages` | 1000 | < 200ms / < 400ms |
| `ConcurrentStageTenTransactions` | 10 (concurrent) | < 50ms / < 100ms |
| `StageLargePayload` | 1MB payload | < 20ms / < 40ms |

#### OutboxPollingBenchmarks.cs

| Benchmark | Messages | Target (P50/P95) |
|-----------|----------|------------------|
| `PollEmptyOutbox` | 0 | < 5ms / < 10ms |
| `PollTenMessages` | 10 | < 10ms / < 20ms |
| `PollHundredMessages` | 100 | < 50ms / < 100ms |
| `PollThousandMessages` | 1000 | < 200ms / < 400ms |
| `RapidPolling` | 10 polls | < 50ms / < 100ms |

#### OutboxPublishingBenchmarks.cs

| Benchmark | Scenario | Target (P50/P95) |
|-----------|----------|------------------|
| `PublishSingleMessage` | 1 message | < 5ms / < 10ms |
| `PublishTenMessages` | 10 messages | < 20ms / < 40ms |
| `PublishHundredMessages` | 100 messages | < 50ms / < 100ms |
| `PublishWithRetry` | Retry cycle | < 10ms / < 20ms |
| `PublishToDeadLetter` | Dead letter | < 10ms / < 20ms |
| `PublishBatchWithRetries` | Batch retry | < 100ms / < 200ms |

**Infrastructure**: SQL Server 2022 (Testcontainers), SqlServerOutboxStore

**Key Metrics**: Staging latency, polling efficiency, publish throughput, retry overhead

---

### 4. Pipeline Benchmarks

**Directory**: `Core/`

Core message dispatch pipeline performance.

#### HandlerInvocationBenchmarks.cs

| Benchmark | Handlers | Target (P50/P95) |
|-----------|----------|------------------|
| `InvokeSingleActionHandler` | 1 | < 1ms / < 2ms |
| `InvokeSingleEventHandler` | 1 | < 1ms / < 2ms |
| `InvokeMultipleEventHandlers` | 5 | < 5ms / < 10ms |
| `InvokeTenEventHandlers` | 10 | < 10ms / < 20ms |

#### MiddlewareChainBenchmarks.cs

| Benchmark | Middleware | Target (P50/P95) |
|-----------|------------|------------------|
| `NoMiddleware` | 0 (baseline) | < 1ms / < 2ms |
| `SingleMiddleware` | 1 | < 2ms / < 4ms |
| `FiveMiddleware` | 5 | < 5ms / < 10ms |
| `TenMiddleware` | 10 | < 10ms / < 20ms |

#### PipelineOrchestrationBenchmarks.cs

| Benchmark | Scenario | Target (P50/P95) |
|-----------|----------|------------------|
| `FullPipelineAction` | Full pipeline (action) | < 5ms / < 10ms |
| `FullPipelineEvent` | Full pipeline (event) | < 5ms / < 10ms |
| `PipelineWithContextPropagation` | Context propagation | < 5ms / < 10ms |
| `PipelineWithCancellation` | Cancellation support | < 5ms / < 10ms |

**Infrastructure**: In-memory, minimal overhead

**Key Metrics**: Handler invocation latency, middleware overhead, context propagation cost

---

### 5. Serialization Benchmarks

**Directory**: `Serialization/`

Serialization performance for message payloads (JSON, MemoryPack, upcasting, encryption).

#### JsonSerializationBenchmarks.cs

| Benchmark | Payload | Target (P50/P95) |
|-----------|---------|------------------|
| `SerializeSmallPayload` | 1KB | < 1ms / < 2ms |
| `DeserializeSmallPayload` | 1KB | < 1ms / < 2ms |
| `RoundTripSmallPayload` | 1KB | < 2ms / < 4ms |
| `SerializeMediumPayload` | 10KB | < 5ms / < 10ms |
| `DeserializeMediumPayload` | 10KB | < 5ms / < 10ms |
| `RoundTripMediumPayload` | 10KB | < 10ms / < 20ms |
| `SerializeLargePayload` | 100KB | < 20ms / < 40ms |
| `DeserializeLargePayload` | 100KB | < 20ms / < 40ms |
| `RoundTripLargePayload` | 100KB | < 40ms / < 80ms |

**Infrastructure**: System.Text.Json

**Key Metrics**: Serialization speed, deserialization speed, round-trip efficiency, allocations

---

### 6. Comparative Benchmarks

**Directory**: `Comparative/`

Head-to-head performance comparison with leading .NET messaging frameworks.

#### MediatRComparisonBenchmarks.cs

**Framework Versions**:
- Excalibur: 1.0.0 (local build)
- MediatR: 13.0+ (latest stable)

**Categories**:

1. **Handler Invocation (Hot Path)**
   - `Dispatch_SingleCommandHandler` vs `MediatR_SingleCommandHandler`
   - `Dispatch_NotificationMultipleHandlers` (3 handlers) vs `MediatR_NotificationMultipleHandlers`
   - `Dispatch_QueryWithReturnValue` vs `MediatR_QueryWithReturnValue`

2. **Concurrent Operations**
   - `Dispatch_ConcurrentCommands10` vs `MediatR_ConcurrentCommands10`
   - `Dispatch_ConcurrentCommands100` vs `MediatR_ConcurrentCommands100`

**Key Findings**:
- Excalibur uses explicit benchmark `AddDispatch(builder => ...)` options (light-mode equivalent, no hidden defaults)
- Both frameworks use identical handler implementations (minimal processing)
- Benchmarks measure framework overhead, not application logic

Latest refreshed values (February 19, 2026):

| Scenario | Dispatch | MediatR |
|----------|----------|---------|
| Single command handler | 118.79 ns | 40.92 ns |
| Notification to 3 handlers | 154.47 ns | 96.10 ns |
| Query with return value | 126.63 ns | 49.29 ns |
| 10 concurrent commands | 1,244.58 ns | 497.81 ns |
| 100 concurrent commands | 12,107.20 ns | 4,797.88 ns |

#### RoutingFirstParityBenchmarks.cs

Scenarios for MediatR + transport replacement patterns where routing is precomputed before dispatch.

Methodology and intent:
- Precompute `IMessageContext.RoutingDecision` before dispatch to isolate routing-first branch overhead.
- Measure local command/query and remote event publish branches in the same benchmark fixture.
- Model transport parity using provider-specific remote buses (`aws-sqs`, `azure-servicebus`, `kafka`, `rabbitmq`) without network I/O.

Latest refreshed values (February 19, 2026):

| Scenario | Mean |
|----------|------|
| `Dispatch_PreRoutedLocalCommand` | 106.0 ns |
| `Dispatch_PreRoutedLocalQuery` | 141.3 ns |
| `Dispatch_PreRoutedRemoteEvent_AwsSqs` | 183.3 ns |
| `Dispatch_PreRoutedRemoteEvent_AzureServiceBus` | 191.8 ns |
| `Dispatch_PreRoutedRemoteEvent_Kafka` | 189.1 ns |
| `Dispatch_PreRoutedRemoteEvent_RabbitMq` | 184.1 ns |
| `Dispatch_PreRoutedRemoteEvent_Kafka_Throughput` | 253.2 ns |
| `Dispatch_PreRoutedRemoteEvent_Kafka_Retry` | 240.5 ns |
| `Dispatch_PreRoutedRemoteEvent_Kafka_Poison` | 228.3 ns |
| `Dispatch_PreRoutedRemoteEvent_Kafka_Observability` | 321.2 ns |
| `Dispatch_PreRoutedRemoteEvent_RabbitMq_Throughput` | 250.9 ns |
| `Dispatch_PreRoutedRemoteEvent_RabbitMq_Retry` | 242.3 ns |
| `Dispatch_PreRoutedRemoteEvent_RabbitMq_Poison` | 232.2 ns |
| `Dispatch_PreRoutedRemoteEvent_RabbitMq_Observability` | 321.4 ns |

#### WolverineComparisonBenchmarks.cs

**Framework Versions**:
- Excalibur: 1.0.0 (local build)
- Wolverine: 3.0+ (latest stable)

**Categories**:

1. **Handler Invocation**
   - `Dispatch_SingleCommandHandler` vs `Wolverine_SingleCommandHandler`
   - `Dispatch_NotificationMultipleHandlers` (3 handlers) vs `Wolverine_NotificationMultipleHandlers`
   - `Dispatch_QueryWithReturnValue` vs `Wolverine_QueryWithReturnValue`

2. **Concurrent Operations**
   - `Dispatch_ConcurrentCommands10` vs `Wolverine_ConcurrentCommands10`
   - `Dispatch_ConcurrentCommands100` vs `Wolverine_ConcurrentCommands100`

3. **Async Message Handling**
   - `Dispatch_AsyncMessageHandling` vs `Wolverine_AsyncMessageHandling`

**Key Findings**:
- Wolverine specializes in async message handling with durable messaging
- Excalibur focuses on lightweight in-process dispatch
- Benchmarks highlight different architectural approaches

**Competitive Positioning**:

| Scenario | Excalibur Strength | Competitor Strength |
|----------|----------------------------|---------------------|
| In-Process Dispatch | ✅ Ultra-lightweight | MediatR: Mature ecosystem |
| Event Sourcing | ✅ Native support | Both: No native support |
| Outbox Pattern | ✅ Native support | Both: Third-party libraries |
| Async Messaging | Excalibur framework | Wolverine: Advanced features |
| Memory Efficiency | ✅ Minimal allocations | Varies |

---

### 7. Diagnostic Slowdown Isolation Benchmarks

**Directory**: `Diagnostics/`

These suites focus on root-cause analysis when end-to-end comparative results regress.

**Latest matrix run**: February 19, 2026 (`class-loop total time: 01:13:06.6268006`, `executed benchmarks: 317`).

#### DispatchHotPathBreakdownBenchmarks.cs

| Benchmark Area | Purpose |
|----------------|---------|
| Dispatcher entry | Isolate dispatch front-door overhead |
| Middleware invoker | Measure chain invocation overhead independent of handler logic |
| Final dispatch handler | Quantify route selection + final publish path cost |
| Local message bus | Measure handler dispatch and cache-hit bypass costs |
| Handler activator/invoker | Separate DI activation from invocation overhead |
| Handler registry lookup | Track lookup hit-path overhead |

Latest values:

| Measurement | Mean | Allocated |
|-------------|------|-----------|
| Dispatcher: Single command | 100.092 ns | 160 B |
| Dispatcher: Query with response | 128.962 ns | 392 B |
| MiddlewareInvoker: Direct invoke | 59.696 ns | 280 B |
| FinalDispatchHandler: Action | 142.505 ns | 304 B |
| LocalMessageBus: Send action | 81.292 ns | 88 B |
| HandlerActivator: Activate | 45.335 ns | 24 B |
| HandlerActivator: Activate (precreated context) | 19.957 ns | 24 B |
| HandlerInvoker: Invoke | 8.557 ns | 0 B |
| HandlerRegistry: Lookup | 3.472 ns | 0 B |

#### HandlerInvokerPathBenchmarks.cs

| Measurement | Mean | Allocated |
|-------------|------|-----------|
| HandlerInvoker: precompiled cache-hit | 31.05 ns | 96 B |
| HandlerInvoker: runtime fallback (cached) | 31.71 ns | 96 B |

#### MiddlewareCostCurveBenchmarks.cs

| Dimension | Values |
|-----------|--------|
| Middleware depth | 0, 1, 3, 5, 10 |
| Message shape | Command, Query, Event |
| Cache mode | Hit, Miss |

Purpose: produce a cost curve that exposes incremental overhead per middleware layer.

Latest depth curve (depth 0 vs depth 10):

| Scenario | Depth 0 | Depth 10 | Allocated (depth 0 -> 10) |
|----------|---------|----------|-----------------------------|
| Command (miss) | 123.0 ns | 1,112.6 ns | 592 B -> 4600 B |
| Command (hit) | 172.9 ns | 1,102.2 ns | 832 B -> 4664 B |
| Query (miss) | 162.6 ns | 1,142.4 ns | 824 B -> 4704 B |
| Query (hit) | 215.0 ns | 1,081.2 ns | 1064 B -> 4536 B |
| Event (miss) | 156.3 ns | 1,185.0 ns | 616 B -> 4672 B |
| Event (hit) | 154.7 ns | 1,108.9 ns | 616 B -> 4672 B |

#### HandlerResolutionBenchmarks.cs + HandlerFanOutBenchmarks.cs

| Dimension | Values |
|-----------|--------|
| Handler lifetime | Transient, Scoped, Singleton |
| Registry lookup | Warm hit, cold miss |
| Fan-out counts | 1, 3, 10, 50 handlers |

Purpose: isolate resolution pressure and event fan-out scaling.

Latest values:

| Measurement | Mean |
|-------------|------|
| Resolve action handler (Transient) | 6.944 ns |
| Resolve action handler (Scoped) | 73.188 ns |
| Resolve action handler (Singleton) | 5.661 ns |
| Dispatch command (Transient) | 109.877 ns |
| Dispatch command (Scoped) | 100.967 ns |
| Dispatch command (Singleton) | 108.866 ns |

| Event handlers | Warm dispatch | Cold dispatch |
|----------------|---------------|---------------|
| 1 | 90.93 ns | 91.21 ns |
| 3 | 143.75 ns | 145.94 ns |
| 10 | 322.83 ns | 325.75 ns |
| 50 | 1,579.10 ns | 1,595.75 ns |

#### TransportAdapterPhaseBenchmarks.cs

| Phase | Description |
|-------|-------------|
| Context mapping | `DefaultMessageMapper` conversion |
| Serialization | `DispatchJsonSerializer` payload serialization |
| Publish | `InMemoryTransportAdapter.SendAsync` |
| Receive + dispatch | `InMemoryTransportAdapter.ReceiveAsync` |
| Ack tracking | In-memory acknowledgment bookkeeping |

Purpose: split adapter-phase costs by payload size and concurrency.

Latest values:

| Phase | Mean range | Notes |
|-------|------------|-------|
| Map transport context | 485.87-501.63 ns | Stable across payload/concurrency |
| Serialize payload | 199.14-9,545.53 ns | Valid rows across all payload/concurrency combinations |
| Publish to in-memory adapter | 9,181.69-9,595.13 ns | Stable across payload/concurrency |
| Receive + dispatch | 256.80-262.36 ns | Stable across payload/concurrency |
| Publish+receive concurrent | 296.50-2,150.33 ns | Concurrency-sensitive, materially improved vs prior run |
| Ack tracking (in-memory) | 50.63-56.93 ns | Stable |

#### FailurePathBenchmarks.cs

| Failure Mode | Description |
|--------------|-------------|
| Retry success path | Transient failures recovered within retry budget |
| Retry exhausted path | Persistent failure and retry exhaustion cost |
| Faulting handler | Exception propagation overhead |
| Cancellation in-flight / pre-canceled | Cancellation overhead split by cancellation mode |
| Dead-letter store | Store/query/replay marker operations |

Purpose: quantify overhead in non-happy-path execution.

Latest values:

| Failure mode | Mean | Allocated |
|--------------|------|-----------|
| Retry: succeeds on third attempt | 30,846,810.9 ns | 1176 B |
| Retry overhead: succeeds on third attempt (zero delay) | 2,981.5 ns | 728 B |
| Retry: exhausted failures | 30,839,850.0 ns | 2946 B |
| Retry overhead: exhausted failures (zero delay) | 7,615.3 ns | 2208 B |
| Dispatch: faulting handler | 2,019.4 ns | 1416 B |
| Dispatch: cancellation in-flight | 8,009.9 ns | 2930 B |
| Dispatch: pre-canceled token | 2,608.6 ns | 1040 B |
| Dead-letter: store message | 634.6 ns | 2352 B |
| Dead-letter: query + replay marker | 1,092.2 ns | 3120 B |

#### LongRunAllocationGcBenchmarks.cs

| Metric | Description |
|--------|-------------|
| Throughput | Operations/sec over sustained windows |
| Latency p50/p95/p99 | Distribution-focused latency signals |
| Allocated bytes/op | Per-operation allocation pressure |
| Gen0 collections/sec | GC churn under sustained load |

Purpose: sustained-window diagnostics for tail-latency and GC behavior.

Latest values:

| Operation count | Window duration mean | Allocated |
|-----------------|----------------------|-----------|
| 10,000 | 4.527 ms | 13.96 MB |
| 50,000 | 22.769 ms | 69.82 MB |
| 100,000 | 45.731 ms | 139.64 MB |

Interpretation note: this suite now reports method-level timing by operation count and can be used for ongoing trend tracking.

---

## Running Benchmarks

### Prerequisites

- **.NET 10.0 SDK** (or later)
- **Docker** (for SQL Server Testcontainers)
- **Release mode** (required for accurate results)

### Full Suite

```bash
cd benchmarks/Excalibur.Dispatch.Benchmarks
dotnet run -c Release
```

### Comparative + Diagnostics Matrix (Non-Interactive, Recommended)

```bash
pwsh ./eng/run-benchmark-matrix.ps1
```

Why this is preferred for refreshes:
- Avoids interactive benchmark selection prompts.
- Avoids overwhelming Wolverine/MassTransit info logs by default.
- Preserves one log file per class for post-failure diagnosis.
- Produces a row-count summary so docs can be synced to a known run.

Expected duration: **10-15 minutes** (includes SQL Server container initialization)
Expected duration (full diagnostics matrix): **65-80 minutes** on the documented test machine.

### Specific Benchmark Class

```bash
# Event Sourcing only
dotnet run -c Release --filter "*EventAppendBenchmarks*"

# Outbox only
dotnet run -c Release --filter "*OutboxStagingBenchmarks*"

# Comparative only
dotnet run -c Release --filter "*ComparisonBenchmarks*"

# Hot-path breakdown only
dotnet run -c Release --filter "*DispatchHotPathBreakdownBenchmarks*"

# Middleware cost curve only
dotnet run -c Release --filter "*MiddlewareCostCurveBenchmarks*"

# Failure-path diagnostics only
dotnet run -c Release --filter "*FailurePathBenchmarks*"

# Retry policy diagnostics only
dotnet run -c Release --filter "*RetryPolicyMicroBenchmarks*"

# Transport concurrency breakdown only
dotnet run -c Release --filter "*TransportConcurrencyBreakdownBenchmarks*"

# Long-run GC/allocation diagnostics only
dotnet run -c Release --filter "*LongRunAllocationGcBenchmarks*"

# Handler invoker path diagnostics only
dotnet run -c Release --filter "*HandlerInvokerPathBenchmarks*"
```

### Filter by Method Name

```bash
# All "Append" benchmarks across all classes
dotnet run -c Release --filter "*Append*"

# All concurrent benchmarks
dotnet run -c Release --filter "*Concurrent*"
```

### Export Formats

Results are automatically exported to `BenchmarkDotNet.Artifacts/results/`:

```
BenchmarkDotNet.Artifacts/
└── results/
    ├── Excalibur.Dispatch.Benchmarks.Patterns.EventAppendBenchmarks-report.html
    ├── Excalibur.Dispatch.Benchmarks.Patterns.EventAppendBenchmarks-report.csv
    ├── Excalibur.Dispatch.Benchmarks.Patterns.EventAppendBenchmarks-report.md
    ├── Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report.html
    └── ... (one set per benchmark class)
```

---

## Performance Targets

Performance targets from `management/specs/testing-and-benchmarking-strategy-spec.md`:

### Event Sourcing

| Operation | Target (P50) | Target (P95) |
|-----------|--------------|--------------|
| Event Append (SQL Server) | < 10ms | < 20ms |
| Aggregate Load (100 events) | < 100ms | < 200ms |
| Snapshot Creation | < 50ms | < 100ms |

### Outbox Pattern

| Operation | Target (P50) | Target (P95) |
|-----------|--------------|--------------|
| Outbox Staging | < 5ms | < 10ms |
| Batch Polling (100 messages) | < 50ms | < 100ms |
| Message Publishing | < 20ms per message | < 40ms per message |

### Core Messaging

| Operation | Target (P50) | Target (P95) |
|-----------|--------------|--------------|
| Dispatch Latency (in-memory) | < 1ms | < 2ms |
| Dispatch Latency (with persistence) | < 10ms | < 20ms |
| Throughput | 10,000+ msg/sec | - |
| Memory Overhead | < 500KB per 1,000 messages | - |

**Note**: Targets are guidelines for v1.0. Actual results may vary based on hardware and workload.

---

## Interpreting Results

### Understanding BenchmarkDotNet Output

**Console Output Example**:

```
| Method                  | Mean      | Error    | StdDev   | Median    | Allocated |
|------------------------ |----------:|---------:|---------:|----------:|----------:|
| AppendSingleEvent       |  8.234 ms | 0.145 ms | 0.128 ms |  8.201 ms |   1.23 KB |
| AppendTenEvents         | 42.567 ms | 0.832 ms | 0.778 ms | 42.412 ms |  12.45 KB |
```

**Key Columns**:

- **Mean**: Average execution time (primary metric)
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of measurements
- **Median**: Middle value (50th percentile)
- **Allocated**: Total memory allocated per operation

### Performance Analysis

#### ✅ Good Performance Indicators

1. **Low Mean**: < target (P50) from performance targets
2. **Low StdDev**: < 10% of Mean (consistent performance)
3. **Low Allocated**: Minimal memory allocations (especially hot paths)
4. **P95 in Range**: P95 (Mean + 2*StdDev) < target (P95)

#### ⚠️ Warning Signs

1. **High StdDev**: > 20% of Mean (inconsistent, investigate outliers)
2. **High Allocated**: Unexpected memory pressure (GC impact)
3. **P95 > Target**: Unacceptable tail latency

### Memory Diagnostics

**Gen0 Collections**:
- 0-1 per operation: ✅ Excellent
- 2-5 per operation: ⚠️ Acceptable
- 5+ per operation: ❌ High GC pressure

**Gen1/Gen2 Collections**:
- 0 per operation: ✅ Required
- Any Gen1/Gen2: ❌ Major performance issue

### Comparative Analysis

**Ratio Column** (comparative benchmarks only):

```
| Method                          | Mean      | Ratio |
|-------------------------------- |----------:|------:|
| Dispatch_SingleCommandHandler   | 118.79 ns | 1.00  | (baseline)
| MediatR_SingleCommandHandler    | 40.92 ns  | 0.34  | (faster)
```

- **Ratio = 1.00**: Baseline (Excalibur)
- **Ratio < 1.00**: Faster than Dispatch (investigate why)
- **Ratio > 1.00**: Slower than Dispatch (expected for competitors)

**Interpreting Competitive Results**:

- **Ratio 0.90-1.10**: Equivalent performance (within 10%)
- **Ratio 1.10-1.50**: Moderate difference (acceptable)
- **Ratio > 1.50**: Significant difference (competitive advantage)

---

## Comparative Analysis

### Market Positioning

Excalibur competes in the **.NET messaging framework** space:

**Primary Competitors**:
1. **MediatR** (market leader, 20M+ NuGet downloads)
2. **Wolverine** (modern alternative, advanced async features)

**Competitive Advantages** (validated by benchmarks):

| Feature | Excalibur | MediatR | Wolverine |
|---------|-------------------|---------|-----------|
| **Event Sourcing** | ✅ Native (SqlServerEventStore) | ❌ No | ❌ No |
| **Outbox Pattern** | ✅ Native (SqlOutboxStore) | ❌ No | ✅ Limited |
| **In-Process Dispatch** | ✅ Ultra-lightweight | ✅ Mature | ⚠️ Heavier |
| **Memory Efficiency** | ✅ Minimal allocations | ✅ Good | ⚠️ Higher |
| **Async Messaging** | ⚠️ Framework layer | ❌ No | ✅ Advanced |
| **CloudEvents** | ✅ Native support | ❌ No | ❌ No |
| **Multi-Cloud** | ✅ AWS/Azure/GCP | ❌ No | ⚠️ Limited |

### When to Choose Excalibur

**Best Use Cases**:
- ✅ **Event-sourced systems** (native aggregate support)
- ✅ **Reliable messaging** (transactional outbox)
- ✅ **Cloud-native applications** (CloudEvents, multi-cloud)
- ✅ **High-throughput in-process dispatch** (minimal overhead)
- ✅ **DDD/CQRS architectures** (opinionated patterns)

**When to Choose Competitors**:
- **MediatR**: Mature ecosystem, existing integrations, simple CQRS
- **Wolverine**: Advanced async messaging, durable queues, scheduling

### Benchmark Methodology

**Fair Comparison Principles**:

1. **Identical Handler Logic**: All frameworks use minimal handler implementations (no application logic)
2. **Same Infrastructure**: .NET 9.0, Release mode, MemoryDiagnoser enabled
3. **Equivalent Configuration**: Excalibur uses explicit benchmark `AddDispatch(builder => ...)` options (light-mode equivalent)
4. **No Middleware**: Baseline benchmarks exclude middleware/behaviors (pure dispatch overhead)
5. **Warm-Up**: BenchmarkDotNet performs automatic warm-up iterations

**What We Measure**:
- Framework overhead (handler resolution, invocation, result handling)
- Memory allocations (DI container, pipeline objects)
- Concurrency scalability (10 and 100 concurrent operations)

**What We Don't Measure**:
- Application logic complexity (all handlers are minimal)
- Database operations (handled separately in Event Sourcing/Outbox benchmarks)
- Network latency (in-process benchmarks only)

---

## Infrastructure

### BenchmarkDotNet Configuration

**Runtime**: .NET 10.0 (`RuntimeMoniker.Net100`)

**Attributes**:
- `[MemoryDiagnoser]`: Tracks Gen0/Gen1/Gen2 collections, allocations
- `[SimpleJob]`: Single runtime configuration (Release mode)
- `[Baseline = true]`: Marks baseline for comparative analysis

**Exporters** (configured in `Program.cs`):
- `MarkdownExporter`: GitHub-friendly tables
- `CsvExporter`: Machine-readable data
- `HtmlExporter`: Interactive charts

### Testcontainers (SQL Server)

**Image**: `mcr.microsoft.com/mssql/server:2022-latest`

**Usage**: Event Sourcing and Outbox benchmarks

**Lifecycle**:
1. `[GlobalSetup]`: Start container, create schema, initialize stores
2. Benchmarks run (warm-up + measurement iterations)
3. `[GlobalCleanup]`: Stop container, dispose resources

**Performance Impact**:
- First run: ~30-60 seconds (Docker image download)
- Subsequent runs: ~10-15 seconds (container startup)

### Test Data

**Event Payloads**:
- **Small**: 1KB (typical domain event)
- **Medium**: 10KB (aggregate with metadata)
- **Large**: 100KB (complex aggregate)
- **Extra Large**: 1MB (stress test)

**Outbox Messages**:
- **Standard**: 1KB JSON payload
- **Large**: 1MB payload (edge case)
- **Metadata**: UserId, TenantId, CorrelationId (real-world structure)

---

## Contributing

### Adding New Benchmarks

1. **Create benchmark class** in appropriate directory:
   - `Patterns/` - Event sourcing, outbox, transport, delivery guarantee operations
   - `Core/` - Core dispatch pipeline, middleware, handler invocation
   - `Serialization/` - Serialization/deserialization (JSON, MemoryPack, encryption, upcasting)
   - `Comparative/` - Competitor comparisons (MediatR, Wolverine, MassTransit)
   - `Optimization/` - Memory allocation, AOT compatibility, pooling

2. **Use standard attributes**:
   ```csharp
   [MemoryDiagnoser]
   [SimpleJob(RuntimeMoniker.Net100)]
   public class MyNewBenchmarks
   {
       [Benchmark(Baseline = true)]
       public void MyBaselineBenchmark() { }

       [Benchmark]
       public void MyComparisonBenchmark() { }
   }
   ```

3. **Follow naming conventions**:
   - Class: `{Category}{Operation}Benchmarks.cs` (e.g., `OutboxStagingBenchmarks.cs`)
   - Method: `{Action}{Scenario}` (e.g., `AppendSingleEvent`, `PollTenMessages`)

4. **Document performance targets**:
   ```csharp
   /// <remarks>
   /// Performance Targets:
   /// - Operation X: < 10ms (P50), < 20ms (P95)
   /// </remarks>
   ```

5. **Update this README**:
   - Add to appropriate benchmark category section
   - Document new benchmark methods and targets
   - Update total benchmark count

### Running Benchmarks in CI/CD

**Example GitHub Actions**:

```yaml
- name: Run MediatR Local Parity Gate
  run: |
    pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes MediatRComparisonBenchmarks,RoutingFirstParityBenchmarks -ArtifactsPath ./BenchmarkDotNet.Artifacts.MediatRParity
    pwsh ./eng/validate-performance-gates.ps1 -Gate MediatRLocalParity -ResultsPath ./BenchmarkDotNet.Artifacts.MediatRParity/results

- name: Run Transport Comparison Gate
  run: |
    pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes WolverineComparisonBenchmarks,MassTransitComparisonBenchmarks -ArtifactsPath ./BenchmarkDotNet.Artifacts.TransportComparison
    pwsh ./eng/validate-performance-gates.ps1 -Gate TransportComparison -ResultsPath ./BenchmarkDotNet.Artifacts.TransportComparison/results

- name: Run Comparative + Diagnostics Matrix
  run: |
    pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore
    # Summary: BenchmarkDotNet.Artifacts/results/benchmark-matrix-summary-*.json
```

**Regression Detection**:
- Store baseline results (CSV) in repository
- Compare new results with baseline
- Fail build if Mean increases > 10% or Allocated increases > 20%

---

## Troubleshooting

### Docker Issues

**Problem**: `Cannot connect to Docker daemon`

**Solution**:
```bash
# Ensure Docker Desktop is running
docker info

# On Windows with WSL2, ensure WSL integration is enabled
```

**Problem**: `SQL Server container startup timeout`

**Solution**:
- Increase timeout in benchmark code (default: 60 seconds)
- Check Docker resources (RAM, CPU allocation)

### Inconsistent Results

**Problem**: High StdDev, unstable benchmarks

**Solution**:
1. Close other applications (reduce system load)
2. Disable CPU throttling / power saving
3. Run multiple times, discard first run (cold start)
4. Check for background processes (Windows Update, antivirus)

### Low Performance

**Problem**: Benchmarks significantly slower than targets

**Solution**:
1. Verify Release mode: `dotnet run -c Release`
2. Check Docker resources (SQL Server needs 2GB+ RAM)
3. Disable antivirus scanning of `bin/` and Docker volumes
4. Use SSD for Docker storage (not HDD)

---

## Summary

This comprehensive benchmark suite provides:

- ✅ **200+ benchmarks** across 7 categories
- ✅ **Real-world scenarios** (SQL Server, concurrency, batching)
- ✅ **Competitive analysis** (MediatR, Wolverine)
- ✅ **Performance baselines** for regression detection
- ✅ **Memory profiling** for GC optimization
- ✅ **Automated export** (HTML, CSV, Markdown)

**Categories**:
- Baseline: Infrastructure validation
- Core: Pipeline, middleware, handler invocation, dispatch throughput
- Patterns: Event sourcing, outbox, transport, delivery guarantees, CDC, inbox
- Serialization: JSON, MemoryPack, upcasting, encryption, span-based
- Comparative: MediatR, Wolverine, MassTransit, pipeline, startup
- Optimization: Memory allocation, AOT, pooling, LINQ elimination, ValueTask

---

**Questions or Issues?** Open a GitHub issue or contact the Excalibur team.

**License**: Excalibur License 1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0 (see `../../LICENSE`)

