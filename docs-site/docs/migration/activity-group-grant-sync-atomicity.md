---
sidebar_position: 24
title: Activity-Group Grant Sync Is Atomic, and Four Providers Must Opt In
description: Synchronizing activity-group grants now replaces a set of grants in one step. The relational and in-memory stores do that natively; on Cosmos DB, DynamoDB, Firestore and MongoDB you must say explicitly that you accept the non-atomic sync, or start-up fails.
---

# Activity-Group Grant Sync Is Atomic, and Four Providers Must Opt In

**Synchronizing activity-group grants from your authorization authority now replaces a set of grants in
one step instead of deleting them and inserting the new ones.** On SQL Server, PostgreSQL and the
in-memory store that happens inside one transaction. On Cosmos DB, DynamoDB, Firestore and MongoDB it
cannot, so **start-up fails until you say which behaviour you want.**

**This page matters to you only if you call one of the three `IActivityGroupService` sync methods.**
Nothing in the framework calls them; they exist for hosts that pull activity groups and grants from a
remote authority. If you do not, the only thing here that affects you is the constructor change at the
bottom, and only if you construct `ActivityGroupService` yourself.

## What to do

**If your grant store is SQL Server, PostgreSQL or the in-memory store, there is nothing to do.** Those
stores replace atomically, and the setting below is ignored for them.

**If your grant store is Cosmos DB, DynamoDB, Firestore or MongoDB, start-up now fails** with a message
naming your store and the setting. Add this to your composition:

```csharp
services.Configure<ActivityGroupSyncOptions>(options =>
    options.GrantSyncAtomicity = GrantSyncAtomicity.BestEffort);
```

That restores exactly the behaviour you had before, and logs one warning at start-up saying so.

## Why it fails rather than quietly carrying on

A grant sync is a **full refresh**: the authority's snapshot becomes the whole set of grants. Without a
transaction spanning the delete and the inserts, there is a window in which the store holds part of the
new set and none of the rest — and because an authorization decision **denies** on a missing grant,
anyone read during that window is refused access the snapshot confers. If the sync fails part-way, the
set stays partial until the next sync succeeds; there is no rollback.

That window is a real cost and you may well decide it is acceptable — a sync that runs at 3am against a
store you accept eventual behaviour from is a reasonable thing to have. What is not reasonable is
inheriting it without being told. So the framework refuses by default and takes `BestEffort` as your
answer, in the same shape Entity Framework Core uses for the same problem on Cosmos.

## What else changed in the sync

**An empty response is now treated differently depending on which sync you called, and the difference
is deliberate.**

| you call | authority returns an empty list | authority returns something that is not a list |
| --- | --- | --- |
| `SyncActivityGroupGrantsAsync(userId)` | **applied** — that user now holds no activity-group grants, and every one of theirs is revoked | refused; nothing is deleted |
| `SyncAllActivityGroupGrantsAsync()` | **refused**; nothing is deleted | refused; nothing is deleted |
| `SyncActivityGroupsAsync()` | **refused**; nothing is deleted | refused; nothing is deleted |

An empty *estate-wide* payload would revoke every user's activity-group grants at once, and an empty
response cannot be told apart from an upstream filter, an authorization scope or a schema change
returning nothing — so it is refused. An empty *per-user* payload is an ordinary state and must be
applied, or a revocation your authority performed never takes effect. A body that did not deserialize
into a list is a failed fetch in every case, and is never read as "this user holds nothing".

**A payload term with leading or trailing whitespace is now refused before anything is deleted.** SQL
Server ignores trailing spaces when it compares keys, so `"Support"` and `"Support "` name one grant
there and two elsewhere; refusing the padding is what makes the same payload mean the same thing on
every provider.

## Two provider fixes you get with this, and they are behaviour changes

Both were found by running these stores against real databases for the first time.

**SQL Server: every grant operation now works.** `GRANT` is a T-SQL key word, so the undelimited table
name the store used was a syntax error in every statement position — `SELECT`, `INSERT`, `UPDATE` and
`DELETE` alike. If you use the SQL Server grant store, every call against it failed with *"Incorrect
syntax near the keyword 'Grant'"*; it now uses the delimited name and works. **No data changes and no
action is required** — there was nothing it could have written.

**PostgreSQL: inserting an activity-group grant now reaches the same table the reads do.** That one
statement addressed a quoted, differently-cased table name, which PostgreSQL treats as a different
table from the one every other statement in the store uses. The call failed with *"relation
"Authz.Grant" does not exist"*. It now uses the same table as the rest. **No action is required.**

## If you implement the store interfaces yourself

`IActivityGroupGrantStore` is unchanged — nothing you have written stops compiling.

A store that can replace a set of grants in one step may additionally implement
`IActivityGroupGrantReplacement`, and the sync will use it. Implementing it is a promise: each member
either applies its whole snapshot or changes nothing, a concurrent reader sees the whole set before or
the whole set after but never a partial one, and two replaces are serialized so the set that remains is
the one whose replace committed last. If your store cannot promise that, do not implement it — set
`GrantSyncAtomicity.BestEffort` instead.

## One constructor changed

`ActivityGroupService` takes an `IOptions<ActivityGroupSyncOptions>` before its logger. If you register
it through `AddExcaliburA3()` you will not notice. If you construct it yourself, add the parameter.
