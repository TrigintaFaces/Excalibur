# Excalibur.Workflows.CodeFixes

Roslyn code-fixes for the `Excalibur.Workflows.Analyzers` determinism diagnostics.

Fixes **EXWF001** by rewriting a non-deterministic API read inside a workflow body to the deterministic `IWorkflowContext` primitive:

```csharp
var now = DateTime.UtcNow;              // before
var now = await ctx.UtcNowAsync(cancellationToken);   // after
```
