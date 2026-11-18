# Load Testing Infrastructure

This directory contains a load-test scaffold. As reviewed on 2026-11-01, the NBomber client simulates dispatch and consumption with delays; actual transport registration is commented out. Its throughput, burst, concurrent and failure modes do not measure Dispatch. The PowerShell runner is also a placeholder: exit code 0 does not establish that any workload ran or passed.

Evidence: `DispatchLoadTestClient.cs` in `Excalibur.Dispatch.LoadTests/`, `scripts/run-load-tests.ps1`, and the campaign census under `artifacts/benchmarks/current-refresh-20261101/`. These cases are unsupported/no-op evidence, not benchmark passes.

## Structure

- `scripts/` - PowerShell and Bash scripts for executing load tests
- `scenarios/` - Test scenario definitions (YAML/JSON)
- `reports/` - Generated performance reports (HTML/Markdown)

## Usage

**Inspect the placeholder runner** (from this directory; does not run tests):
```powershell
.\scripts\run-load-tests.ps1 -Scenario sustained-throughput
```

**Planned report location** (the placeholder does not generate this report):
```powershell
.\reports\latest\index.html
```

## Requirements

- Docker (for test infrastructure)
- .NET 10 SDK pinned by the repository `global.json` (the project targets `net10.0`)
- PowerShell 7+ or Bash

## Scenarios

The following load test scenarios will be implemented in **Phase 4: Performance Optimization**:

- `sustained-throughput` - 1 hour constant load to detect memory leaks and performance degradation
- `burst-traffic` - 10x load spike to test surge capacity and recovery
- `concurrent-consumers` - Multiple consumer testing to verify fair distribution
- `failure-scenarios` - Fault tolerance testing (disconnection/reconnection, handler exceptions, backpressure)
- `mixed-workloads` - Complex scenario testing with various message sizes and handler complexities

## Historical Performance Targets

The following Phase 4 targets are historical aspirations, not verified results from this scaffold:

- **Throughput**: >100,000 messages/second (in-memory transport on commodity hardware)
- **Latency**:
  - <1ms P95 (in-memory transport)
  - <10ms P95 (Kafka/RabbitMQ transports)
- **Allocations**: <100 bytes per message dispatch
- **GC Overhead**: <5% CPU for observability
- **Memory**: No leaks during sustained 1-hour test

## Load Test Deliverables (Phase 4)

1. **Scenarios** (`scenarios/*.yaml`):
   - Scenario definitions with parameters
   - Message templates
   - Load profiles (ramp-up, sustained, burst)

2. **Execution Scripts** (`scripts/`):
   - `run-load-tests.ps1` - Main execution script
   - `setup-infrastructure.ps1` - Docker Compose orchestration
   - `collect-metrics.ps1` - Metrics collection and aggregation

3. **Reports** (`reports/`):
   - HTML dashboards with charts
   - Markdown summaries
   - Baseline comparisons
   - Regression detection

## Infrastructure

Load tests will utilize:
- **Docker Compose** for realistic environments
- **Real transports** (Kafka, RabbitMQ, AWS SQS, Azure Service Bus)
- **Prometheus** for metrics collection
- **Grafana** for visualization (optional)
- **Custom harness** for message generation and validation

## Current Status

**Phase 2: Directory structure created** ✅
**Phase 4: Implementation pending** ⏸️

The Phase 4 schedule is historical planning context. The current scaffold does not establish throughput, latency, allocation, resilience or NativeAOT performance.

---

See the current benchmark guidance in [`../benchmarks/Excalibur.Dispatch.Benchmarks/BENCHMARKS.md`](../benchmarks/Excalibur.Dispatch.Benchmarks/BENCHMARKS.md) for bounded, measured runs.
