# Excalibur.Workflows.Analyzers

Roslyn analyzers that keep durable workflow bodies deterministic.

## EXWF001 — Non-deterministic API in a workflow body

Reading the ambient clock (`DateTime.Now`/`UtcNow`/`Today`, `DateTimeOffset.Now`/`UtcNow`) or generating an identifier (`Guid.NewGuid`) directly inside a `[Workflow]` body produces different values on each run and diverges on deterministic replay. Use the deterministic `IWorkflowContext` primitives instead:

```csharp
// Non-deterministic — flagged EXWF001
var now = DateTime.UtcNow;
var id  = Guid.NewGuid();

// Deterministic — journaled + replayed
var now = await ctx.UtcNowAsync(cancellationToken);
var id  = await ctx.NewGuidAsync(cancellationToken);
```

The companion `Excalibur.Workflows.CodeFixes` package provides an automatic fix.
