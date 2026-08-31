# Async Test Standards

This document codifies the rules for writing deterministic async tests in Excalibur.Dispatch. These patterns were derived from an analysis of flaky test patterns across the repository's worst-offending fixtures.

If this document and `CLAUDE.md` diverge, treat `CLAUDE.md` as authoritative.

---

## The 6 Rules

### Rule 1: No `Task.Delay` as Synchronization

**Bad:**
```csharp
await sut.StartAsync(CancellationToken.None);
await Task.Delay(500); // Hope the work finishes in 500ms
result.ShouldNotBeEmpty();
```

**Good:**
```csharp
var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

// Wire signal into the system under test
processor.OnBatchProcessed += _ => tcs.TrySetResult(true);

await sut.StartAsync(CancellationToken.None);
await WaitHelpers.AwaitSignalAsync(tcs.Task, timeout: TimeSpan.FromSeconds(30));
result.ShouldNotBeEmpty();
```

**Why:** `Task.Delay` creates a race condition. Under CI load, 500ms may not be enough. Increasing the delay slows down tests. Signals complete as soon as the work finishes -- fast on fast machines, patient on slow ones.

---

### Rule 2: No `DisposeAsync()` as Work-Completion Proof

**Bad:**
```csharp
await using var service = new BackgroundService();
await service.StartAsync(CancellationToken.None);
// ... trigger some work ...
await service.DisposeAsync(); // "Proves" work completed because Dispose waits
```

**Good:**
```csharp
var workCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

service.OnWorkCompleted += () => workCompleted.TrySetResult();
await service.StartAsync(CancellationToken.None);
// ... trigger work ...
await WaitHelpers.AwaitSignalAsync(workCompleted.Task, TimeSpan.FromSeconds(30));

// Now dispose for cleanup only
await service.DisposeAsync();
```

**Why:** `DisposeAsync` may return before internal background loops complete, depending on implementation. Explicit completion signals are unambiguous.

---

### Rule 3: No Polling Collection Counts Unless Count IS the Behavior

**Bad:**
```csharp
await sut.StartAsync(CancellationToken.None);
// Poll until 5 items appear
await WaitHelpers.WaitUntilAsync(() => results.Count >= 5, TimeSpan.FromSeconds(10));
```

**Good (if count IS the behavior):**
```csharp
// Testing: "The aggregator emits exactly 3 aggregated batches"
await WaitHelpers.WaitUntilAsync(() => emittedBatches.Count >= 3, TimeSpan.FromSeconds(30));
emittedBatches.Count.ShouldBe(3);
```

**Good (if count is NOT the behavior):**
```csharp
// Testing: "Messages are processed successfully"
var allProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
int processedCount = 0;
processor.OnMessageProcessed += () =>
{
    if (Interlocked.Increment(ref processedCount) >= expectedCount)
        allProcessed.TrySetResult();
};
await WaitHelpers.AwaitSignalAsync(allProcessed.Task, TimeSpan.FromSeconds(30));
```

**Why:** Polling collection counts is a proxy for the real behavior. If the real behavior produces a signal (callback, event, TCS), use that signal directly for more precise and faster tests.

---

### Rule 4: Use Fake Clocks Where Feasible

When a system under test depends on `TimeProvider` (or `ISystemClock`), inject `Microsoft.Extensions.Time.Testing.FakeTimeProvider` to control time without wall-clock waits.

```csharp
var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
var sut = new ScheduledService(fakeTime);

await sut.StartAsync(CancellationToken.None);

// Advance time by 5 minutes instantly -- no wall-clock wait
fakeTime.Advance(TimeSpan.FromMinutes(5));

// Assert the scheduled work ran
result.ShouldNotBeNull();
```

**When NOT to use fake clocks:**
- When the system under test does not accept a `TimeProvider` parameter
- When real timing IS the product behavior (e.g., benchmarks, rate limiters under real load)
- When the fixture already uses TCS signals and is deterministic without fake clocks

**Note:** `Microsoft.Extensions.Time.Testing` requires adding the package to the test project's `.csproj`. Check whether the fixture's project already references it before adding.

---

### Rule 5: Timing-Based Tests Must Justify Wall-Clock Behavior

If a test uses real time (e.g., `Stopwatch`, `DateTimeOffset.UtcNow`), the test comment must explain why:

```csharp
// Wall-clock timing is the product behavior: we're testing that the
// rate limiter delays at least 100ms between bursts
var sw = Stopwatch.StartNew();
await rateLimiter.WaitAsync(CancellationToken.None);
sw.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(80)); // 20ms jitter budget
```

Tests without this justification will be flagged during review.

---

### Rule 6: Use Generous Scaled Timeouts

All `AwaitSignalAsync` / `WaitUntilAsync` calls must use timeouts calibrated for CI runners, not developer machines.

| Scenario | Minimum Timeout |
|----------|----------------|
| Simple signal (TCS) | 10 seconds |
| Background service startup + signal | 30 seconds |
| Multiple batch cycles | 60 seconds |
| GC / memory assertions | 1 MB threshold |
| Background service polling | 2000ms+ window, 100ms intervals |

**Why:** CI runners under full suite load (50,000+ tests) can be 3-10x slower than a developer laptop. Tight timeouts cause false failures.

---

## Utilities

### `WaitHelpers` (Tests.Shared)

Located at `tests/Shared/Tests.Shared/Infrastructure/WaitHelpers.cs`.

| Method | Purpose |
|--------|---------|
| `WaitUntilAsync(Func<bool>, TimeSpan, TimeSpan?)` | Poll a condition with timeout |
| `AwaitSignalAsync(Task, TimeSpan, TimeSpan?)` | Wait for a TCS signal with timeout |
| `AwaitSignalAsync<T>(Task<T>, TimeSpan, TimeSpan?)` | Wait for a typed TCS signal with timeout |

Default poll interval: 100ms (20ms for signal awaits).

### `TaskCompletionSource` Patterns

Always use `TaskCreationOptions.RunContinuationsAsynchronously`:

```csharp
// Non-generic (void signal)
var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
signal.TrySetResult();

// Generic (carries a value)
var signal = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
signal.TrySetResult(batch.ToList());
```

**Why `RunContinuationsAsynchronously`?** Without it, `TrySetResult()` runs the continuation inline on the calling thread. If that thread holds a lock, this causes deadlocks. `RunContinuationsAsynchronously` schedules the continuation on the thread pool.

### Thread-Safe Collections for Multi-Callback Tests

When a test accumulates results from multiple callbacks:

```csharp
var results = new ConcurrentBag<string>();
processor.OnItem += item => results.Add(item);

// ... run test ...

results.Count.ShouldBe(expectedCount);
```

Use `ConcurrentBag<T>` or `ConcurrentQueue<T>`, never `List<T>`.

### Preventing Parallel Execution

Fixtures that stress shared resources (CPU, GC, memory) should use xUnit collection isolation:

```csharp
[Collection("Performance Tests")]
public class BatchProcessorShould
{
    // Tests in this class won't run in parallel with other
    // classes in the "Performance Tests" collection
}
```

---

## Checklist for New Async Tests

Before submitting a PR with async tests, verify:

- [ ] No `Task.Delay` used for synchronization (Rule 1)
- [ ] No `DisposeAsync` used as proof of work completion (Rule 2)
- [ ] Collection count polling justified by behavior-under-test (Rule 3)
- [ ] Fake clocks considered if `TimeProvider` is available (Rule 4)
- [ ] Wall-clock timing usage justified in comments (Rule 5)
- [ ] Timeouts are CI-generous (Rule 6)
- [ ] `TaskCompletionSource` uses `RunContinuationsAsynchronously`
- [ ] Thread-safe collections used for concurrent result accumulation
- [ ] Performance-sensitive fixtures use `[Collection(...)]` isolation

---

## Soak Testing

After rewriting async fixtures, soak-test to confirm determinism:

```bash
# Run a specific fixture 10 times consecutively
for i in $(seq 1 10); do
  dotnet test <project.csproj> --filter "FullyQualifiedName~FixtureName" --no-build -c Release
  if [ $? -ne 0 ]; then echo "FAIL on run $i"; exit 1; fi
done
echo "All 10 runs passed"
```

The soak run that established these rules exceeded this requirement: 50/50 consecutive runs across all five rewritten fixtures with 0 failures.

---

## References

- Test Quality Rules: `CLAUDE.md` (Test Quality Rules section)
- WaitHelpers: `tests/Shared/Tests.Shared/Infrastructure/WaitHelpers.cs`
