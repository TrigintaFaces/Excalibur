; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Analyzer%20Diagnostics.md

## Release 1.0.0

### New Rules

Rule ID  | Category                          | Severity | Notes
---------|-----------------------------------|----------|-------
EXWF001  | Excalibur.Workflows.Determinism   | Error    | Non-deterministic API (ambient clock, identifier and random generation, wall-clock delays, elapsed-time counters) used inside a workflow body; use the deterministic IWorkflowContext primitive, or compute the value in an activity, instead
