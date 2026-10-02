---
sidebar_position: 17
title: A Destroyed Key Is Stated, Never Inferred
description: The crypto-shredding read path produces an erasure tombstone only from a durable destruction record, never from a key backend's answer. IKeyDestructionStatusProvider is back to one member, and a crypto-shredding composition now refuses to start without a key-destruction ledger.
---

# A Destroyed Key Is Stated, Never Inferred

A `null` from `IFieldEncryptor.DecryptAsync` is not "no data". It is a statement that the field was
lawfully crypto-shredded — that the key protecting it is gone and the ciphertext can never be opened
again. See [GDPR Erasure](../compliance/gdpr-erasure.md).

**The read path used to produce that statement from the absence of a key**, and absence is not
destruction. A key-management backend with a recovery window reports a deleted-but-fully-restorable key
exactly as it reports one that never existed. Azure Key Vault answers a soft-deleted key as *not found*
for its entire retention period — 90 days by default — while a single `recover` call brings it back. For
that whole window, every read of the affected subject's personal fields returned a tombstone claiming the
data was erased, over data that was not erased and that anyone with vault permissions could restore.

The rule now is one sentence: **a tombstone is produced only where a durable destruction record, written
by the actor that performed the destruction, says that this envelope's key generation was destroyed.**
That record lives in `IKeyDestructionLedger`, and the read path consults nothing else.

:::warning This page supersedes an earlier revision of itself — delete what it told you to add

An earlier revision of this page said `IKeyDestructionStatusProvider` had gained a **version-scoped**
`IsKeyDestroyedAsync(string keyId, int version, CancellationToken)` overload, that the read path asked it,
and that *"it is a compile error until you do"*. A revision of
[Field envelope names its key generation](field-envelope-names-its-key-generation.md) said the same of a
**generation-scoped** overload.

**Both overloads have been removed. `IKeyDestructionStatusProvider` declares one member again** — the
handle-scoped `IsKeyDestroyedAsync(string keyId, CancellationToken)` — and nothing on the read path calls
it. If you added either overload on that advice, delete it; it is now an unused member on your own type.
Nothing breaks if you leave it, and nothing calls it.

The reason is in [Why no backend can answer this](#why-no-backend-can-answer-this) below: narrowing the
question from the handle to the version, and then to the generation, moved it closer to the right subject
without ever making it answerable — a backend holds no information that separates *destroyed* from *never
held here*, at any granularity. A record of the destruction does.
:::

## What changed

| | Before | After |
|---|---|---|
| Source of the tombstone | the key backend's answer about a key | a **row in `IKeyDestructionLedger`** for the envelope's key generation |
| `IKeyDestructionStatusProvider` | a second, version-scoped member | **one member**, handle-scoped, and the read path never calls it |
| Who calls `IKeyDestructionStatusProvider` | the read path and erasure verification | **erasure verification only**, about a handle, once, at completion |
| `DecryptAsync`, generation has a ledger row | — | `null` tombstone |
| `DecryptAsync`, generation has no ledger row | `null` tombstone when the key was absent | the decrypt is **attempted**; it succeeds, or it **throws** |
| `DecryptAsync`, ledger unreachable | — | **throws**; an unreadable ledger is never "not destroyed" |
| `AddCryptoShredding()` with no ledger registered | started, failed on the first encrypted read | **refuses to start**, naming the remedy |

## What you must do

### Register a key-destruction ledger

`AddCryptoShredding()` and `AddEventSourcingCryptoShredding()` both require one, and **neither registers
it**. A composition without one now refuses to start.

An erasure store supplies the ledger from the same instance it registers the store from, so for a
deployment that erases there is nothing extra to add — only an erasure store to make sure is there:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddCryptoShredding();

// One of these. Each registers IKeyDestructionLedger alongside IErasureStore.
services.AddInMemoryErasureStore();     // development
services.AddPostgresErasureStore(/* ... */);
services.AddSqlServerErasureStore(/* ... */);
```

A deployment that encrypts personal data at rest and **never destroys a subject key** — an encrypted event
store, inbox or outbox in a deployment that runs no erasure subsystem — names that instead:

```csharp
// INSTEAD of AddCryptoShredding(), and never alongside an erasure store.
services.AddCryptoShreddingWithoutErasure();
```

That registers a ledger holding no rows, so every generation reports as not destroyed. In a deployment
that destroys nothing, that is the true answer rather than a stand-in: reads decrypt normally and no
tombstone is ever produced.

**It is a separate call and deliberately not a default.** A ledger that always answers "not destroyed"
cannot fabricate an erasure, which is the safe direction — and that is exactly why defaulting to one would
be wrong. A deployment that *does* erase but whose erasure store was never registered would then silently
never tombstone anything, and read its own erased subjects back in the clear, with nothing to report it.

### Two start-up refusals, and what each message means

| Registration | Outcome |
|---|---|
| Crypto-shredding, no ledger at all | **Refused.** `InvalidOperationException` naming `IKeyDestructionLedger`, the three erasure-store calls, and `AddCryptoShreddingWithoutErasure()` as the alternative. |
| `AddCryptoShreddingWithoutErasure()` **and** an erasure store | **Refused.** The two answer the same question differently. |
| The container supplies no `IServiceProviderIsService` | **Refused.** Registration could not be probed, so the question was not answered, and reporting success would turn *not measured* into *verified*. |

The second refusal is worth its own sentence, because the alternative is silent. Every ledger registration
in this framework is a `TryAdd`, so with both calls present the winner is whichever ran first — and in one
of the two orders an always-false ledger stands in front of a real erasure store. That deployment performs
erasures, never tombstones anything, and no component reports a problem. Order-dependence on this question
is refused rather than documented.

The check runs from both an `IHostedService` and an `IStartupPrerequisiteValidator`, so it fires for a host
that starts normally and for a consumer who builds a provider and calls `ValidateStartupGates`. A consumer
who does neither still gets a dependency-injection error on the first resolve.

### Apply the ledger schema on SQL Server and PostgreSQL

The ledger is two tables, and the erasure store verifies its schema at start-up and fails fast naming what
is missing. The DDL, what the migration does to existing rows, and the shipped script names are in
[Erasure destroyed-key record](erasure-destroyed-key-record.md).

## Why no backend can answer this

A key backend can report one thing: whether it can serve or restore some material now. The negative of
that covers three states at once — **destroyed**, **recoverable**, and **never held here** — and it cannot
separate them, because a backend retains nothing about material it never held. That is permanent and by
construction, not a gap in any particular product.

So "I cannot serve this key" is a three-valued answer collapsed into one `false`, and two of its three
meanings must not produce a tombstone. Deriving an erasure from it computes a different function from the
one the guarantee names, and its failure mode is the catastrophic one: recoverable personal data reported
as lawfully erased, with nothing downstream able to tell.

The information that separates those states existed at one instant and belonged to one actor — whoever
destroyed the material. So the answer comes from a record that actor wrote.

### The generation, not the handle and not a version ordinal

A data subject's key handle is derived from the subject, so it is **stable and re-mintable**. Destroy a
subject's key, let one ordinary write provision another at the same handle, and both the handle and the
restarted version ordinal report "not destroyed" — truthfully, about material the reader is not holding.
The erased subject's older fields then read as live and fail their authentication tag, surfacing as
corrupted data rather than as the erasure they are.

A **generation** is minted once, from a cryptographic random source, and never reused, so a destroyed
generation stays destroyed whatever is provisioned at that handle afterwards. The field envelope carries
it, and the ledger is keyed on it alone. See
[Field envelope names its key generation](field-envelope-names-its-key-generation.md).

### A row's existence is the statement

There is no status column, no flag and no timestamp comparison in the ledger. A row is written only after a
destruction completed irreversibly, so the question a read asks is whether the row is there — which leaves
no clause to forget and nothing to misinterpret.

Two consequences follow, and both are load-bearing:

- **No row is `false`. Being unable to *read* the ledger is an exception.** The two are different outcomes
  and are never collapsed. A `false` returned because the ledger was unreachable would be
  indistinguishable, in logs and in metrics, from "asked, and there is no row" — and absence read as an
  answer is the whole history of this defect.
- **An affirmative answer is monotone.** Rows are only ever added; there is no update and no delete, so a
  destroyed generation cannot become undestroyed. An implementation may cache a `true` indefinitely and
  needs no invalidation for it. A `false` means "no row *yet*" and may be held only briefly.

One more scope limit, because it is easy to over-read: a `true` says the plaintext behind **this envelope**
is unrecoverable. It does not say the subject is erased. A plaintext copy held elsewhere, or a second
ciphertext written under a different key, leaves this answer correct and an erasure claim false.

## What `IKeyDestructionStatusProvider` is still for

It has not been removed, and it still matters — for a different question, with a different caller.

**Erasure verification** asks it, about a **handle**, once, at erasure completion: *does your backend still
hold any recoverable copy of this handle's material?* `IErasureVerificationService.VerifyKeyDeletionAsync`
never falls back to a key lookup, so a provider that does not supply the capability never has an erasure
that destroyed its keys certified — the request stays awaiting destruction. See
[An erasure certificate states only what the erasure established](erasure-certificate-states-what-it-established.md).

**A read must never consult it**, and the reason is now visible in the interface's own documentation: a
handle's state comes apart from a particular ciphertext's in two ordinary ways. A handle split internally —
one version destroyed or trimmed while a later one lives, which rotation produces and which HashiCorp Vault
and AWS KMS both permit — answers `false` although the material an older envelope names is gone. A handle
re-occupied after a destruction holds live material, so it answers `false` while an older envelope's key is
gone.

All five key providers in the box implement the capability, and its contract is unchanged: return `true`
only on a backend statement that no recoverable copy remains, never from a lookup that merely failed to
produce an answer, and throw when you cannot obtain an answer at all.

## If you destroy keys outside this framework

A tombstone requires a ledger row, and the framework's own erasure path is the only thing that writes one
on your behalf. So a consumer who provisioned or destroyed keys through their own process, or who migrated
data erased before adopting this framework, has **no row for those generations** — and a read of that
ciphertext **fails loudly** rather than reporting an erasure.

That is intended, not a gap to work around: this framework will not claim an erasure it did not perform.
Record those destructions yourself:

```csharp
Task RecordDestroyedGenerationAsync(string keyGeneration, CancellationToken cancellationToken);
```

**This is an assertion you own.** By calling it you assert that an irreversible destruction of that
generation's material has already completed. On the strength of that row alone the framework will report
every field encrypted under that generation as lawfully erased — to your users and on your compliance
evidence — and it will not re-verify the assertion, not then and not at read time. If the material still
exists, or can be restored, you have published a false erasure claim.

Two rules for calling it:

- **Call it after the destruction returns, never before.** A row written ahead of the destruction reports
  live material as erased for however long the gap lasts, which is the one failure this ledger exists to
  make impossible.
- **Read the generation first and keep it.** On most backends you cannot read it afterwards, because the
  identifier is backend material the destruction takes with it. Read, keep, destroy, then record.

Recording the same generation twice is a no-op rather than an error, since a generation is minted once and
names one destruction. A failure to write throws and is never swallowed — a caller who believed it recorded
a destruction it did not would see that subject's reads fail with no indication why. On the no-erasure
ledger the call is **refused**: there is nowhere durable to put the row, and a destruction that is not
recorded cannot be reported as an erasure.

The framework's own erasure path does not use this member, and that separation is deliberate. An erasure
this framework performs stages the generation before destroying it and records it only once the destruction
is established, so its rows rest on the framework's own observation of both halves. This member rests on
your assertion, and nothing re-verifies it.

## If you catch exceptions around field decryption

These throw rather than returning a tombstone:

- **No registered provider supports the envelope's algorithm** — an `EncryptionException`. A configuration
  fault, never a lawful erasure.
- **The envelope declares an earlier format version** — an `EncryptionException` with `ErrorCode =
  EncryptionErrorCode.InvalidCiphertext`. See
  [Field envelope names its key generation](field-envelope-names-its-key-generation.md).
- **The envelope carries no key generation** — the same error code. The material behind it cannot be
  identified, so a destroyed subject cannot be told from one whose key was provisioned again.
- **The generation has no ledger row and the decrypt fails** — whatever the provider raises, including
  `EncryptionErrorCode.KeyNotFound`. **The data may still be there.** The ledger has already said this
  generation was not destroyed here, so the read fails rather than tombstoning; investigate the key
  backend, and recover a soft-deleted key that should be live rather than working around the error.
- **The ledger could not be read** — the implementation's own exception, naming the cause. Never reported
  as either answer.

None of these replaces a tombstone for a genuinely erased subject: that case still returns `null`.

## After upgrading

Nothing about stored personal data changes, and no backfill of ciphertext is needed. What changes is which
reads answer `null`: only those whose generation this deployment holds a destruction record for.

**If you are upgrading an existing SQL Server or PostgreSQL erasure store, read
[Erasure destroyed-key record](erasure-destroyed-key-record.md) before you run anything.** The migration
replaces the destroyed-keys table, and past erasure requests report an empty destroyed-handle breakdown
afterwards. Signed completion certificates are untouched and still verify.

**If you ran against Azure Key Vault with soft-delete enabled and have performed erasures, treat any past
read that returned a tombstone during a key's retention window as unverified.** The key may have been
recoverable at the time. Purge protection plus an elapsed retention window is what makes a destruction
irreversible, and the erasure-verification report is what attests it.

## Related

- [Crypto-shredding](../compliance/crypto-shredding.md) — registration, the read path, and the fail-closed contract
- [Field envelope names its key generation](field-envelope-names-its-key-generation.md) — the identifier the ledger keys on
- [Erasure destroyed-key record](erasure-destroyed-key-record.md) — the ledger's two tables and their DDL
- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
