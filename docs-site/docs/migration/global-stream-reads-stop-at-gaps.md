---
title: Global-stream reads stop at the first gap
sidebar_label: Global-stream reads stop at gaps
description: Why a global-stream subscriber can now stop advancing, how to tell, and what to do about it.
---

# Global-stream reads stop at the first gap

A read of the global event stream now returns only the **contiguous run** starting immediately after
your position, and stops at the first missing position. Previously it returned whatever the query
found.

This closes a defect in which a subscriber could **permanently skip a committed event**. It also
introduces a failure mode you need to be able to recognise, and that is what this page is for.

## Why the change

The event store guarantees that at every instant the committed global positions form a contiguous
prefix. That is a property of a **state**. A subscriber's scan **spans states** — a
`SELECT ... WHERE Position > @checkpoint ORDER BY Position` examines each slot at a different moment
and never returns to one it has passed.

One writer is enough to lose an event:

1. the scan passes slot `N` while `N` is still uncommitted,
2. `N` commits,
3. `N+1` commits,
4. the scan reaches `N+1` and returns it.

Your subscriber advances its high-water mark past `N`, and no later read from that checkpoint ever
revisits it. No error, no retry, nothing downstream able to detect the omission.

Truncating at the first gap removes that: a position that is missing right now belongs to a
transaction still in flight, so the read stops rather than handing you anything above it.

## What changes for you

**If your stream has no permanent holes, nothing changes** except that a read may return fewer events
than before and pick the rest up on the next poll. That is normal and healthy.

**If your stream has a permanently absent position, your subscriber stops advancing at it and stays
there.** It does not skip past. This is deliberate — a stall is recoverable and a skipped event is
not — but it is a real operational state you need to be able to see.

### Where a permanent hole comes from

Archival used to `DELETE` event rows. It now tombstones them in place, so no new hole can appear. A
database archived by an earlier version can still carry them.

## How to tell this is what is happening

The symptom is **a projection or subscription that stops advancing while the host looks healthy**:
no events delivered, no error, no unhealthy status. The host treats "no events found" as idle, because
from its side that is indistinguishable from a quiet stream.

To confirm, raise the log level:

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.EventSourcing.Queries.ContiguousGlobalStreamQuery": "Debug"
    }
  }
}
```

A read that stopped short logs the position it expected, the next committed position it actually
found, and how many events it delivered. **One such line is normal** — it is the healthy signature of
a concurrent append that has not committed yet. **The same gap repeating across every poll is the
actionable condition.**

:::warning This is not yet surfaced as health or a metric
Persistent-gap detection needs state the read path does not hold, so today the only signal is the
Debug log above. A stalled subscriber will not fail a health check and will not raise a warning. If
you run global-stream projections, watch checkpoint progress as an operational metric rather than
relying on the framework to tell you.
:::

## A permanent hole cannot arise on the current schema

A stream provisioned by the shipped create script cannot carry a permanent hole. Archival **tombstones**
a row rather than deleting it: the position survives with a null payload and `ArchivedAt` set, and every
consumer of the global stream already skips a null-payload row and advances past it. So a missing
position always means a transaction still in flight, which is the case this page's stall is designed for
— the read resumes on its own once that transaction commits or aborts.

Permanent holes were only reachable on a database whose archival predates the tombstone shape. There is
no in-place upgrade from that shape; re-provision from the shipped create script.

## `ReadByEventTypeAsync` is deliberately NOT gap-filtered

A read filtered to one event type returns a **legitimately sparse** set of positions: the gaps are
simply the events of other types. A gap there carries no information about whether anything is in
flight, so truncating would discard committed events that will never be redelivered.

**Consequence you must handle:** if you advance a high-water mark from `ReadByEventTypeAsync`, you are
still exposed to the skip described at the top of this page. Either drive your checkpoint from
`ReadAllAsync` and filter in your own handler, or accept that the filtered read is at-most-once with
respect to a concurrent writer.

## Related

- [Event store](../event-sourcing/event-store.md) — the counter-row position allocation and its cost
- [Global stream projection host](../event-sourcing/global-stream-projection-host.md)
- [Known issues](../known-issues.md)
