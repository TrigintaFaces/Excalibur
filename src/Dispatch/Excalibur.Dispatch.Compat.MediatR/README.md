# Excalibur.Dispatch.Compat.MediatR

A **migration compatibility surface** for [Excalibur.Dispatch](https://github.com/TrigintaFaces/Excalibur).

Moving an existing codebase that targets the MediatR API onto Excalibur.Dispatch? This package provides
**source-compatible interfaces** with the same shapes that MediatR-based code references, forwarding them
to the canonical Excalibur.Dispatch primitives. In most cases code compiles after a **namespace swap**:

```diff
- using MediatR;
+ using Excalibur.Dispatch.Compat.MediatR;
```

Combined with the `Excalibur.Dispatch.Analyzers` migration code-fixes (`EXMIG####`), the swap is largely
mechanical. This package is an interoperability/migration aid — it is **not** a re-implementation of
MediatR, and it does not reproduce MediatR's internals or runtime behavior beyond the published interface
shapes consumer code depends on.

## What it provides

The interface shapes used by MediatR-based code, mapped to canonical Excalibur.Dispatch types:

| Compatibility interface | Maps to canonical Dispatch |
|-------------------------|----------------------------|
| `IRequest<TResponse>` / `IRequest` | `IDispatchAction<TResponse>` |
| `IRequestHandler<TReq,TRes>` / `IRequestHandler<TReq>` | `IActionHandler` / `IDispatchHandler` |
| `INotification` / `INotificationHandler<T>` | event + `IEventHandler` |
| `IPipelineBehavior<TReq,TRes>` | `IDispatchMiddleware` |
| `IStreamRequest<T>` / `IStreamRequestHandler<,>` | `IStreamingDispatcher` |
| `IMediator` / `ISender` / `IPublisher` | `IDispatcher` |

## Design notes

- **Separate, isolated package.** Depends on `Excalibur.Dispatch`; the canonical packages never depend on it.
- **AOT-safe.** Registration via source generation — no reflection scan on the consumer path, no
  `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` on any consumer-facing API.
- **Public by design.** The compatibility types are a consumer-facing migration surface (see ADR-341).

See `management/architecture/adr-341-mediatr-compat-surface-policy.md` for the full policy.

## Trademark & affiliation notice

> **DRAFT — pending legal-team review (S857).**
>
> "MediatR" is a trademark of its respective owner. This package is **not affiliated with, sponsored by,
> or endorsed by** the MediatR project or its owner. References to "MediatR" are used solely to describe
> interoperability — that is, to identify the third-party API that this package helps existing code
> migrate away from. Excalibur.Dispatch.Compat.MediatR is an independent work distributed under the
> Excalibur project license; it is not derived from MediatR's source.
