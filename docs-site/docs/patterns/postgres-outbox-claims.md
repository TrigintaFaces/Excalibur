---
title: PostgreSQL Outbox Claim Completion
description: Complete the exact live PostgreSQL acquisition while preserving caller transaction ownership
---

# PostgreSQL Outbox Claim Completion

The PostgreSQL outbox exposes acquisition-scoped sent, failure, backoff and dead-letter completion through `IClaimScopedOutboxStore`, `IFencedClaimScopedOutboxStore`, `IClaimScopedDeadLetterableOutboxStore` and `IFencedDeadLetterableOutboxStore`. Resolve these through the store's `IServiceProvider` capability seam, including when a decorator wraps the store.

Capture the message ID and `DispatcherId` immediately after acquisition, before invoking delivery callbacks. A same-process reclaim creates a different acquisition. Its predecessor must retain its original evidence and cannot complete the successor's work. Fenced completion also carries the processing operation's captured positive tenure in `OutboxWriteAuthority`.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

public static class PostgresClaimCompletion
{
    public static async Task<OutboxCompletionOutcome> Run(IOutboxStore store, OutboundMessage message, Func<OutboundMessage, CancellationToken, Task> publishAsync, CancellationToken cancellationToken)
    {
        var completion = (IClaimScopedOutboxStore?)
            ((IServiceProvider)store).GetService(typeof(IClaimScopedOutboxStore))
            ?? throw new InvalidOperationException("The store lacks claim-scoped completion.");

        var messageId = message.Id;
        var acquisition = message.DispatcherId
            ?? throw new InvalidOperationException("The message has no acquisition evidence.");

        await publishAsync(message, cancellationToken);
        return await completion.MarkSentAsync(messageId, acquisition, cancellationToken);
    }
}
```

Only `Applied` reports a state transition. `ClaimLost` means the current live acquisition no longer matches; `MessageNotFound` means no main row was observed. Sent deletes the row, and dead-letter moves it to the archive, so a later completion reports missing rather than inventing a retained terminal state. `FenceRefused` takes precedence and concerns the captured leadership tenure. A valid newer tenure can advance its durable high-water even when the row-level outcome refuses.

Each strict completion is one dependent PostgreSQL statement: scope authority, locked preimage, post-lock server time, exact acquisition eligibility, mutation and outcome. Byte comparison preserves case, accents, Unicode normalization differences and trailing spaces independently of text collation. Lease time is evaluated after acquiring the row lock, including when another transaction held the row until the lease expired.

Plain failure preserves the greater of stored and supplied attempts. Scheduled failure retains PostgreSQL's existing increment-by-one behavior. Both release the lease and retain a server-anchored failure visibility floor; a computed backoff can extend that floor. Dead-letter copies the original tenant, payload, metadata and creation time, retains the greater attempt count and deletes the source atomically. Suppressed required insert/delete work fails the statement instead of claiming completion or committing an orphaned copy.

## Caller transaction ownership

The store uses its existing borrowed connection. It does not create a second connection or commit, roll back or dispose a caller's transaction. Under autocommit, the statement is atomic. In an explicit caller-owned transaction, `Applied` is provisional until that caller commits; rollback undoes both its fence advance and row/archive changes. A statement error can abort the caller's transaction, which remains the caller's responsibility.

Legacy ID-only, token-only, process-prefix and administrative methods retain their weaker direct-use contracts. They cannot substitute for strict completion after a refusal. Transport delivery remains at-least-once; handlers still require idempotency. This ownership protocol alone does not establish payload/header serializer or driver NativeAOT support.

See [Outbox Pattern](outbox.md) and [Dead Letter Handling](dead-letter.md) for registration and recovery composition.
