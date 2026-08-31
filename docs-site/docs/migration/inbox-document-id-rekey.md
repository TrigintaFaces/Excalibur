---
sidebar_position: 6
title: Firestore and Elasticsearch Inbox Keys Change Shape
description: A one-time re-key of existing inbox entries on the Firestore and Elasticsearch inbox stores, and how to upgrade without re-processing messages.
---

# Firestore and Elasticsearch Inbox Keys Change Shape

The Firestore and Elasticsearch inbox stores address each entry by a document id built from the tenant,
the message id, and the handler type. **That id is now composed differently, so entries written by an
earlier version are not found by this version.**

**This page matters to you only if you run the inbox on Firestore or Elasticsearch.** No other inbox
provider changed. If you are adopting either store for the first time, there is nothing to do.

## What changed, and why it could not stay compatible

The previous id joined the three terms with `_`:

```
acme_corp_42_MyApp.Handlers.OrderHandler
```

Nothing separated a term that *contains* `_` from the separator between terms, so the join was
**ambiguous** — two different messages could produce one id:

| Tenant | Message id | Handler | Rendered id |
| --- | --- | --- | --- |
| `acme` | `corp_42` | `H` | `acme_corp_42_H` |
| `acme_corp` | `42` | `H` | `acme_corp_42_H` |

This is the id the store uses to decide *"have I already handled this message?"*. When two messages
render one id, the second is read as an already-seen duplicate and **dropped** — with no error, no
retry, and nothing in your logs to investigate, because from the store's point of view everything
succeeded. An inbox exists to not drop messages, so this is corrected rather than documented.

Each term is now percent-encoded before the join, and the separator is `:`:

```
acme:corp_42:MyApp.Handlers.OrderHandler
acme_corp:42:MyApp.Handlers.OrderHandler
```

Encoding removes the separator from the terms themselves, so the two ids above are now distinct — and
every distinct message keeps a distinct id, whatever characters your tenant identifiers and message ids
contain.

**On Firestore the id additionally carries a constant `inbox:` prefix**, giving
`inbox:acme:corp_42:MyApp.Handlers.OrderHandler`. Firestore reserves document ids matching `__.*__` and
rejects a write that uses one; because a deployment without multi-tenancy carries the reserved tenant
term `__untenanted__`, every such id would otherwise begin `__` and the write would fail outright for any
handler type name ending in `__`. The prefix is constant, so it does not affect which messages are
distinct. Percent-encoding also escapes `/`, which Firestore reads as a path separator — an unescaped one
in a message id previously wrote the entry to a nested path the matching read never looked in.

**There is no read-side fallback to the old id, deliberately.** A store that tried the new id and then
the old one would keep both shapes live indefinitely: nothing would ever retire the ambiguous form, and
the guarantee this change exists to provide — that two messages can never share an entry — would be
unverifiable for as long as the fallback remained. The change is a one-time migration instead.

## Two new ways a write can be refused

Both are deliberate, both fail loudly at the point of composition, and neither can drop a message
silently. They are listed here because a deployment that worked before the upgrade can hit them after it.

### The id has a length limit, and encoding makes ids longer

Elasticsearch refuses a document `_id` over **512 bytes**; Firestore's limit is 1500 and is unlikely to
bind. Percent-encoding expands anything outside `A-Z a-z 0-9 - . _ ~`, and `_` — the old separator — was
never expanded, so **ids that fitted before can exceed the limit now**. Generic type names are where this
bites: measured, a nested closed generic type name encodes to about **1.24x** its unencoded length.

A worked example, measured rather than estimated — a GUID tenant, a GUID message id, and an
assembly-qualified `Dictionary<string, List<string>>` as the handler type composes to **665 bytes**, which
Elasticsearch rejects. The same handler type as a plain `FullName` (not assembly-qualified) composes to
about 551 bytes and is also over. One nesting level less fits comfortably.

If your handler type names are long — deeply nested generics, or assembly-qualified names — check them
before upgrading. The framework raises an error naming the limit, the actual size, and what to shorten.
**The id is never truncated to fit**, because two different messages could then share one key, which is
the defect this change exists to remove.

### A malformed message id is rejected rather than merged

A term containing an **unpaired surrogate** — half of a character pair, which usually arrives from
truncating a string at the wrong boundary — is refused. This is not tidiness: the percent-encoder maps
*every* unpaired surrogate onto the same replacement character, so admitting them would let two different
message ids render one key and silently drop the second, which is exactly the bug being fixed. If you see
this error, something upstream is cutting message ids mid-character.

## Before you upgrade

Pick whichever of these fits your deployment. **The first is simplest and is the recommended one.**

### Option 1 — drain the inbox, then upgrade

An inbox entry is a *transient* record, not a permanent one: it exists to recognise a redelivery, and
cleanup removes processed entries once they pass your retention cutoff. So the cheapest migration is to
let the old entries become irrelevant before you upgrade.

1. Stop accepting new messages, and let in-flight ones finish so no entry is left `Received`,
   `Processing`, or `Failed`.
2. Wait out — or run cleanup to enforce — your retention window, so the remaining processed entries are
   deleted. `CleanupAllTenantsProcessedEntriesAsync` deletes processed entries older than the cutoff you
   pass it.
3. Upgrade and restart.

Entries written after the upgrade use the new id, and there are no old ones left to miss.

### Option 2 — re-key the existing entries

If you cannot drain — for example, a large backlog of unprocessed entries you do not want to re-receive
— copy each entry to its new id and delete the old document. The id is part of the document's identity
on both stores, so this is a **write-and-delete, not an update**.

Every document carries its own `messageId` and `handlerType` as fields, which is what makes this
mechanical. **Recover the tenant term from the old id using those two fields** — do not read it from a
`tenantId` field, which is written only on some paths and is absent on entries created by the claim and
mark-processed paths. Because the old id is exactly `tenant` + `_` + `messageId` + `_` + `handlerType`,
and you know the last two exactly, whatever precedes them is the tenant:

```csharp
var suffix = $"_{messageId}_{handlerType}";          // both read from the document's own fields

if (!oldId.EndsWith(suffix, StringComparison.Ordinal))
{
    continue;   // not on the old shape - already migrated, or written by the new version
}

var tenantId = oldId[..^suffix.Length];

var newId = $"{Uri.EscapeDataString(tenantId)}:{Uri.EscapeDataString(messageId)}:{Uri.EscapeDataString(handlerType)}";

// Firestore only - the constant prefix described above. Elasticsearch has no prefix.
var firestoreId = "inbox:" + newId;
```

`Uri.EscapeDataString` is percent-encoding in the RFC 3986 sense — everything outside `A-Z a-z 0-9 - . _ ~`
is escaped. If you are migrating from outside .NET, use your platform's equivalent and check that it
escapes `:` and `%`.

The `EndsWith` check is also the discriminator for *"has this document been migrated yet?"*: an old id has
`_` immediately before the handler type, a new one has `:`, so the two shapes cannot be confused. A
deployment that does not use multi-tenancy will find `__untenanted__` in the recovered `tenantId` — that is
the framework's reserved sentinel and is carried through unchanged.

Write the document under `newId`, then delete the old one. Run this with the inbox quiesced, so nothing
is writing entries while you move them.

### If you skip the migration

Nothing fails at startup, and no data is deleted. The old entries simply stop being visible to the
store: a message whose entry was written before the upgrade is no longer recognised as already handled,
so if it is redelivered it will be processed again. Whether that matters depends on how idempotent your
handlers are — the inbox is what was making them idempotent for you. Entries left in a non-processed
state are also no longer picked up for retry.

Old documents are not removed by the upgrade. Once you are satisfied the migration is complete, you can
delete anything left on the previous id shape — identified by the same `EndsWith` check as above, not by
guessing from the id alone.

## A note on id conventions in this framework

This subsystem now has more than one convention for composing a document id, because each was introduced
with a different constraint in mind and existing stored data made changing the earlier ones costly. The
inbox stores on Cosmos DB, MongoDB, Redis, and DynamoDB join with `:` and escape only `%` and `:`;
Firestore and Elasticsearch now percent-encode in full and Firestore carries a prefix; other subsystems
use other shapes again. They are individually sound and are never compared with one another, so this is
not a correctness problem — but it is more variation than a reader should have to hold, and consolidating
it is worth doing deliberately rather than leaving it to be rediscovered. Recorded here so it is known
rather than found.

## See also

- [Inbox pattern](../patterns/inbox.md)
- [Multi-tenancy](../multi-tenancy.md)
