# Excalibur.Dispatch.Patterns — Serialization Policy (Core)

Summary

- Core uses MemoryPack for internal/default serialization. Verify allocations and native compatibility for the selected wrapper APIs and payload contracts.
- JSON support remains available via the `IJsonSerializer` abstraction and hosting packages (e.g., Patterns.Hosting.Json).

Key points

- `DistributedCacheExtensions` defaults to MemoryPack for `Get/Set` without a serializer.
- Overloads with `IJsonSerializer` continue to support JSON use cases.
- `BackpressureMiddleware` emits JSON using `IJsonSerializer` when registered; otherwise the health endpoint returns plain-text guidance to register the hosting JSON package.
- Claim Check serializer requires a base serializer or the `IJsonSerializer`‑based constructor; STJ fallback is removed from core.

Why

- Avoids System.Text.Json in core hot paths; keeps JSON at the public boundary.
- Serializer separation provides a choice of implementations; it does not establish a performance improvement or NativeAOT compatibility for the complete application. See the [MemoryPack verification scope](../Excalibur.Dispatch.Serialization.MemoryPack/README.md#performance-characteristics).

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
