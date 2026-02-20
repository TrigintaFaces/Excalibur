# Load Testing Infrastructure

This directory contains load testing scenarios, execution scripts, and performance reports for Excalibur.

## Structure

- `eng/` - PowerShell and Bash scripts for executing load tests
- `scenarios/` - Test scenario definitions (YAML/JSON)
- `reports/` - Generated performance reports (HTML/Markdown)

## Usage

**Run Load Tests**:
```powershell
.\eng\run-load-tests.ps1 -Scenario sustained-throughput
```

**View Reports**:
```powershell
.\reports\latest\index.html
```

## Requirements

- Docker (for test infrastructure)
- .NET 9 SDK
- PowerShell 7+ or Bash

## Scenarios

The following load test scenarios will be implemented in **Phase 4: Performance Optimization**:

- `sustained-throughput` - 1 hour constant load to detect memory leaks and performance degradation
- `burst-traffic` - 10x load spike to test surge capacity and recovery
- `concurrent-consumers` - Multiple consumer testing to verify fair distribution
- `failure-scenarios` - Fault tolerance testing (disconnection/reconnection, handler exceptions, backpressure)
- `mixed-workloads` - Complex scenario testing with various message sizes and handler complexities

## Performance Targets

From `REFACTORING_PROMPT.md` Phase 4:

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

2. **Execution Scripts** (`eng/`):
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

This infrastructure will be fully implemented during **Phase 4: Performance Optimization (Week 7-10)** as outlined in REFACTORING_PROMPT.md.

---

See `REFACTORING_PROMPT.md` Section 4.4 for complete load testing requirements.
