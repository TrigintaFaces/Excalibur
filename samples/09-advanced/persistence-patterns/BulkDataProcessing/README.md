# Sequential bulk processing with inline projections

Run from the repository root:

```powershell
dotnet run --project samples/09-advanced/persistence-patterns/BulkDataProcessing -c Release -p:BuildExamplesAndTests=true
```

This .NET 10 managed sample runs the same two-record workload with `DataProcessor<int>` and
`BulkDataProcessor<int>`. Each handler saves an aggregate through the real event-sourced repository.
An inline projection persists one account view per event. Assertions check:

- Regular processing updates projections before each checkpoint callback.
- Bulk processing saves events and calls checkpoint callbacks in the same sequential order, while
  projections remain unchanged until the final catch-up.
- Both modes finish with two persisted projections and two checkpoint callbacks.

The example registers the real projection processor, event types, subscription scope and in-memory
stores. It deliberately does not start background projection workers, which would race the assertions.
No database connection is used. In-memory stores and callbacks demonstrate the checkpoint protocol,
not crash-durable persistence. Production must use durable stores/checkpoint callbacks and idempotent
handlers: failures during final catch-up can replay the last page after restart.

Choose bulk mode only when no handler needs to read projections updated earlier in the run. Catch-up
cancellation or exceptions fail the run; other non-caught-up results warn and allow record completion.
Successful record completion is not a freshness guarantee. Existing stall and total deadlines include
catch-up, without artificial progress checkpoints.

When replacing an application-owned bulk wrapper, remove its final `ProcessPendingAsync` call and
inherit the framework base directly to avoid duplicate catch-up. Registration remains explicit and
the constructor/fetch contract is unchanged.

This is a correctness example, not a benchmark or a NativeAOT claim. The separate native sample's
`DataProcessingSmoke.cs` checks the framework bulk branch with a deterministic projection-service
stand-in; that does not certify this entire in-memory projection composition for NativeAOT.
