# Dispatcher.cs Split Plan

## Current State

`Dispatcher.cs` is ~82KB -- the largest file in the codebase. It contains:
- Handler resolution and caching
- Pipeline construction and invocation
- Context creation and management
- Routing logic
- Streaming dispatch
- Progress dispatch
- Direct local dispatch optimization

## Proposed Seams

| Component | Responsibility | Est. Size |
|-----------|---------------|-----------|
| `DispatcherRouting` | Transport routing, qualifier resolution | ~15KB |
| `DispatcherPipelineFactory` | Pipeline construction, middleware chain | ~10KB |
| `DispatcherHandlerResolver` | Handler resolution, caching, factory delegates | ~20KB |
| `DispatcherContextFactory` | Context creation, ambient context management | ~10KB |
| `Dispatcher` (core) | Coordination, public API surface | ~15KB |
| `StreamingDispatcher` | Streaming + progress operations | ~12KB |

## Approach

Incremental extraction via `partial class` first (low risk), then separate classes with explicit dependency injection in a future sprint.

**Do NOT attempt this refactoring in a single sprint.** File individual Beads tasks for each extraction.
