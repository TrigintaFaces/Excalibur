# Excalibur.Dispatch.Patterns — Serialization Policy (Core)

Summary

- Core uses MemoryPack for internal/default serialization to align with R0.14 (AOT‑friendly, low allocations).
- JSON support remains available via the `IJsonSerializer` abstraction and hosting packages (e.g., Patterns.Hosting.Json).

Key points

- `DistributedCacheExtensions` defaults to MemoryPack for `Get/Set` without a serializer.
- Overloads with `IJsonSerializer` continue to support JSON use cases.
- `BackpressureMiddleware` emits JSON using `IJsonSerializer` when registered; otherwise the health endpoint returns plain-text guidance to register the hosting JSON package.
- Claim Check serializer requires a base serializer or the `IJsonSerializer`‑based constructor; STJ fallback is removed from core.

Why

- Avoids System.Text.Json in core hot paths; keeps JSON at the public boundary.
- Improves AOT readiness and performance while maintaining flexibility.
