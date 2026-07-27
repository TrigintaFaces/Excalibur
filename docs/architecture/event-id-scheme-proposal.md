# Event ID Scheme Proposal (SUPERSEDED)

> **Status: SUPERSEDED — not a specification, do not reference for allocation.**
>
> The authoritative Event ID allocation is [`event-id-strategy.md`](./event-id-strategy.md) (and ADR-095).

## Why this document is a pointer now

This 2026-01 proposal sketched a 5-digit, Microsoft-style semantic scheme (10xxx Core, 20xxx Transport,
30xxx Middleware, …). The scheme that was **actually adopted differs**: dedicated per-package ranges under
`3000-4999`, with duplicate-ID conflicts remapped into the `50000+` / `70000+` ranges. Retaining this
proposal's detailed range tables would create a **second, divergent source of truth** for Event IDs, so
they have been removed.

## Where Event IDs live now

- **Authoritative allocation table** — [`event-id-strategy.md`](./event-id-strategy.md). Find your
  package's reserved range there and use the next free ID.
- **Decision record** — ADR-095.
- Each package declares its IDs in its own `*EventId.cs` within the range assigned by
  `event-id-strategy.md`.

*Retained only for provenance of the consolidation effort; it does not describe the shipped scheme.*
