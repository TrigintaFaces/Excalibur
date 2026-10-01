---
title: A key version reports its own creation instant, or reports that it has none
sidebar_label: Key creation instants are reported, not invented
description: KeyMetadata.CreatedAt is now nullable. It carries the instant of the version it describes, or null where the backend supplies none -- never a substituted clock. Readers must treat unknown as stale.
---

# A key version reports its own creation instant, or reports that it has none

**`KeyMetadata.CreatedAt` is now `DateTimeOffset?`.** If you read it, your code stops compiling until you
decide what an unknown creation instant means — and this note tells you which answer is safe.

1. **On HashiCorp Vault, the instant now belongs to the version being described.** It was read from the
   handle's *first* version for every version, so every version of a handle reported one identical date.
2. **A provider that cannot learn the instant now reports `null`.** It previously substituted the local
   clock, which is a value no caller can tell from a measurement.

## Why the first change matters

A key handle holds a lineage of versions. Reading the first version's instant for all of them made the
field unable to **order** versions: two versions of one handle compared equal, so any caller sorting or
selecting by it received an arbitrary element rather than the one it asked for.

If you compare `CreatedAt` across versions of a single handle on Vault, that comparison was returning
equality and is now meaningful. **Re-check any logic you wrote around it** — in particular a comparison you
may have added to work around the equality, which will now behave differently because the underlying values
finally differ.

Across *handles* the field was already correct, so active-key selection and key-age checks are unaffected.

## Why the second change matters more

Substituting `UtcNow` for an unknown instant is not a degraded answer. It is an **inverted** one: the value
whose age nobody knows sorts ahead of every value whose age is known, so the material of unknown age is
selected as the newest.

The effect is worst on the AWS historical-key provider, whose whole purpose is choosing the version that
was live at a requested instant. It walks the versions in order and stops at the first one that postdates
the request — so a version stamped with the local clock appears to postdate every historical request,
**truncates the scan, and resolves to an earlier version than the one that was live**, reporting success.
Measured against the previous build: a lookup for September, against a key created in January and rotated
in June with the rotation undated, returned **version 1**.

A fabricated instant cannot be distinguished from a measured one by any caller, so nothing downstream ever
learns the answer was invented. Failing is loud and recoverable; the alternative is silent and was not.

## What to do where you read it

**Treat an unknown instant as STALE, never as recent.** That is the direction every reader inside the
framework now takes, and the reasoning transfers to yours: a fabricated instant was dangerous precisely
because it sorts a version as the NEWEST, so stale material reads as current and is never replaced. Null must
therefore never resolve to "recent".

| what you are deciding | with `null` |
|---|---|
| is this key old enough to rotate? | **yes, rotate it** — nothing shows it is within the maximum age |
| has it exceeded a maximum age? | **yes** — "cannot be shown to be inside" is not "inside" |
| which of these keys is newest? | the undated one sorts **oldest**, so it is never chosen as newest |
| when is it next due? | **already due** — never a future instant |

**Watch out for the lifted comparison, which picks the opposite.** `keyAge > MaxKeyAge` over a null
`TimeSpan` evaluates to `false`, so the obvious code reports an undatable key as NOT due and the absence
silently takes the unsafe direction. Decide it explicitly:

```csharp
// WRONG — an undatable key is never rotated
var age = DateTimeOffset.UtcNow - key.CreatedAt;
if (age > maxKeyAge) { Rotate(); }

// RIGHT — unknown counts as exceeded
if (key.CreatedAt is not { } createdAt || DateTimeOffset.UtcNow - createdAt > maxKeyAge) { Rotate(); }
```

**Do not reach for a sentinel.** Substituting `DateTimeOffset.MinValue` or `UtcNow` to keep the old shape
reintroduces exactly the defect this change removes: a value a caller cannot distinguish from a measurement.

**You are unlikely to meet a null in practice.** Vault Transit dates most versions, Key Vault sets
`CreatedOn`, and KMS sets `CreationDate` — but "most" is not "all", which is why the type now says so.

## One case still fails rather than returning null

`IHistoricalKeyProvider.GetKeyVersionAsync` — the point-in-time lookup — throws when a version cannot be
dated, and that is deliberate. There, ordering **is** the operation: the lookup selects the version live at a
requested instant by comparing creation instants, and stops at the first version that postdates it. An
undatable version makes the question unanswerable, and the scan would silently resolve to an earlier version
than the one that was live. Refusing a question that has no correct answer is right; refusing an ordinary
read you can answer is not.

## If you implement a key-management provider yourself

Nothing forces a change — `KeyMetadata.CreatedAt` is unchanged. But the obligations are worth stating,
because both defects above were in shipped providers:

- **Report the instant of the version the metadata describes**, not the handle's first version. If your
  backend dates versions individually, read the one you are describing.
- **Never substitute your own clock, or a sentinel, for an instant the backend did not give you.** Report
  `null`. A consumer comparing two of your values has no way to tell which were measured.
- **Do not fail the read either.** Describing a key the backend will not date is still a valid read, and a
  throw there breaks the ordinary path for every such key. Report the absence and let the caller resolve it.

## Related

- [Crypto-shredding](../compliance/crypto-shredding.md) — how a data subject's key is minted, used and destroyed
- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
