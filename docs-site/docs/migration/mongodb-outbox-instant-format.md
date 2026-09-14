---
sidebar_position: 8
title: MongoDB Outbox Timestamps Change Shape
description: The MongoDB outbox now stores every instant as a BSON date. Messages already sent by an earlier version are not expired by the TTL index until you rewrite them.
---

# MongoDB Outbox Timestamps Change Shape

Every instant on a MongoDB outbox message — `createdAt`, `scheduledAt`, `sentAt`, `lastAttemptAt`,
`nextAttemptAt` and `leasedAt` — is now stored as a **BSON date**. An earlier version stored each one as the driver's
default sub-document for a `DateTimeOffset`:

```json
{ "DateTime": ISODate("2026-01-14T09:12:04.117Z"), "Ticks": NumberLong("638..."), "Offset": 0 }
```

**This page matters to you only if you run the outbox on MongoDB and are upgrading a collection that
already holds messages.** A new collection is written in the new shape from the first message, and there
is nothing to do.

## Why it changed

The claim query decides which messages are due using the **server's** clock, and a comparison against
`$$NOW` can only be expressed against a date. Compared against a sub-document, the predicate was
unconditionally true — every message looked due, whatever its lease said.

## What still works, without you doing anything

The store reads **both** shapes wherever it compares an instant: on the claim path, on the scheduling
gate, and on the admin queries. A message staged before the upgrade is therefore claimed, scheduled,
gated and swept on the instant it actually carries. Delivery is not affected by the mixed collection.

## The one thing that does not: TTL expiry of already-sent messages

The TTL index is declared over `sentAt`, and MongoDB's expiry monitor **acts only on a date**. It skips a
sub-document silently — there is no error and no log line, the documents simply stay.

So, for messages your deployment **already marked sent before the upgrade**:

| How you reclaim sent messages | Effect on pre-upgrade sent messages |
| --- | --- |
| The retention sweep (`CleanupAllTenantsSentMessagesAsync`, and the retention contributor that calls it) | **Removed.** The sweep reads both shapes. |
| The TTL index alone, with no sweep configured | **Retained indefinitely.** They are never expired. |

If you rely on the TTL index alone, the collection grows without bound until you either enable the
retention sweep or rewrite the legacy instants in place.

## Rewriting the legacy shape in place

Do this **after** your rollout has completed and every instance is running the new version. While an
older instance is still running, it writes new documents in the old shape, so a rewrite would leave work
behind.

**1. Count what you actually have.** Substitute your collection name if you changed
`MongoDbOutboxOptions.CollectionName` from its default:

```js
db.outbox_messages.countDocuments({ sentAt: { $type: "object" } })
```

A result of `0` means there is nothing to rewrite and you can stop here.

**2. Rewrite on a copy first, and confirm the result.** The update below reads the date out of the
sub-document and replaces the field with it. Run it against a restored copy of the collection, then run
the count from step 1 again and confirm it reports `0`:

```js
db.outbox_messages.updateMany(
  { sentAt: { $type: "object" } },
  [ { $set: { sentAt: "$sentAt.DateTime" } } ]
)
```

**3. Apply it to the live collection.** The update is idempotent — documents already carrying a date do
not match the filter — so it is safe to re-run if it is interrupted.

No other instant carries a TTL index, and the store reads both shapes for every instant it compares —
`scheduledAt`, `nextAttemptAt`, `leasedAt` and `lastAttemptAt` — so rewriting those is optional.
`createdAt` is stored but never compared, so its shape does not affect behaviour at all. If you want the
collection uniform, repeat steps 1-3 for each of them.

## During the rollout

While an upgraded instance and an earlier one are both running, MongoDB's query operators are
type-bracketed in the other direction too: a lease stamped as a date by an upgraded dispatcher is not
visible to the comparisons an earlier dispatcher makes. If an upgraded instance crashes mid-rollout, its
in-flight messages are not re-claimed by an older instance until that older instance is retired. **No
message is lost or duplicated** — recovery is delayed for the length of the rollout. Retiring the older
instances promptly keeps that window short.
