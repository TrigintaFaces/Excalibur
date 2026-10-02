# Architecture — Excalibur.Compliance (erasure, legal hold, and SOC 2 attestation)

This document states what the erasure and legal-hold subsystem guarantees, how it achieves it, and —
just as importantly — what it does **not** yet prove. It is a contract, not a description: every claim
below is meant to be falsifiable, and where we cannot falsify one we say so rather than assert it.

## Guarantees

### Tenant isolation

**Under multi-tenancy, a read of an erasure request or a legal hold returns rows owned by the ambient
tenant, plus rows explicitly recorded as estate-wide. It returns no other tenant's rows.**

This holds for all six shipped stores — SQL Server, PostgreSQL and in-memory, for both contracts.

**A caller-supplied `tenantId` can only narrow that result, never widen it.** The argument is ANDed onto
the ambient term rather than replacing it, so two equality terms can only intersect: a caller naming
another tenant gets the empty set, and a caller naming none still gets their own ambient scope. Widening
would require changing an `AND` to an `OR`, which is a one-token change a reviewer can see.

### Legal holds block erasure, and a missing tenant does not lift them

**An active legal hold covering a data subject blocks that subject's erasure, and a hold check made
without a tenant consults estate-wide holds rather than none.**

The second half is the load-bearing part. An estate-wide hold carries no data-subject identifier, and the
subject query matches on that identifier — in SQL a null never equals a value — so the subject path can
never return one. If the estate-wide lookup is also skipped, the check sees **zero** holds and reports
nothing blocking. Erasure is irreversible, so that failure is unrecoverable in the direction that matters.

### Reading and mutating a hold are asymmetric, deliberately

**A tenant sees an estate-wide hold. A tenant cannot modify one.**

A tenant must see it, because it blocks that tenant's erasures. A tenant must not modify it: a mutation
matching an estate-wide row would let one tenant re-home an estate-wide preservation order into its own
partition, silently lifting it for every other tenant — whose next erasure then proceeds and reports
success. Reads use *owned-or-estate-wide*; mutations use strict ownership.

### Releasing a hold is a compare-and-set, so a hold extended under a sweep is not released

**An update to a legal hold is applied only while the stored record still carries the `LegalHold.Version`
the caller read. If the record moved in between, the write is refused, nothing is stored, and
`LegalHoldConcurrencyException` is raised. In particular: a hold whose expiry is extended between the
expiration sweep's read and its write REMAINS ACTIVE, with the extended expiry, and the sweep reports the
conflict at warning level rather than absorbing it.**

Releasing is a read-modify-write — read the hold, decide, write the whole record back — so without a
version check the decision lands over whatever the record became in the meantime. A hold is the authority
that stops an erasure destroying records a controller is legally obliged to keep, so the consequence of the
lost update is not a lost hold but lost records, irreversibly. And it is silent: the released record is
well-formed and carries a plausible reason, so an auditor inspecting it afterwards sees a legitimate
auto-release with no trace that an extension ever existed.

The comparison is a term of the same statement that writes (`version = @ExpectedVersion` in the relational
providers' `UPDATE`; `ConcurrentDictionary.TryUpdate` against the compared instance in memory), never a
read the store performs first — a store that reads the version, compares it, then writes has only moved the
race a few microseconds later.

**On a conflict, callers re-read and re-decide; they do not retry.** Repeating the same write against a
fresh version reinstates the lost update. The expiration sweep re-reads and, if the hold is no longer
expired, makes no change at all — the correct outcome when an operator has just extended it.

**The sweep reports every outcome, and on an instrument an operator can alert on.** It logs each conflict
at warning level with the hold and case, and counts every hold it considered on
`dispatch.legal_hold.expiration.outcomes`, tagged `outcome` = `released` / `extended` / `contended` /
`missing` / `failed`. The counter exists because the log alone is not enough: a log line is read by
somebody already looking, and a hold whose release keeps losing a concurrency check is exactly the
condition nobody is looking at — left unresolved it stays active past the period it was filed for.
**Alert on a sustained non-zero `contended`**; `extended` is normal operation.

How it is verified: `LegalHoldStoreConformanceTestKit.UpdateHoldAsync_StaleVersion_ShouldThrowAndLeaveHoldIntact`
binds the refusal for every provider (the reader takes a copy, a second writer extends the expiry, the
reader's stale release is refused and the extension survives), with
`UpdateHoldAsync_Succeeding_ShouldIncrementVersion` as its liveness twin — an uncontended update is still
applied and advances the version, so a store that refuses everything cannot pass. At the sweep level,
`ExtendingAHoldDuringTheExpirationSweepShould` asserts both halves against the real in-memory store: the
hold stays active with its extended expiry and the conflict is reported, and a genuinely expired hold with
no contending writer is still released.

Consumer obligation: round-trip `LegalHold.Version` unchanged. Building the record to write with a `with`
expression over the one that was read carries it across; constructing a fresh `LegalHold` from parts
discards it and the write will be refused rather than silently applied.

Known gap: a database provisioned before the version column existed has no column for the store's
statements to bind. The store's schema verification fails closed at startup and names the missing column;
it does not migrate the table.

### Field encryption is idempotent, so a retried write cannot double-wrap a value

**Encrypting an annotated record twice yields the same stored value as encrypting it once. A caller that
retries the same instance after a failed write does not produce a value wrapped in two envelopes.**

This matters because the encrypt path mutates the caller's own object in place and is followed by a write
that can fail. When it does, the caller still holds an instance whose annotated fields have already been
replaced by envelopes, and the natural response to a transient fault is to retry that same instance. A
write path that could not tell an envelope from plaintext would encrypt it a second time, and the stored
value would then decrypt in one pass to an envelope string rather than to the subject's data — a
corruption that presents as a *successful* decryption, which is the worst shape it could take.

The guard keys on the envelope marker, not on the presence of a value. An **unmarked** value is the
legacy form written before the marker existed, so it is still encrypted rather than skipped; treating
unmarked as already-encrypted would leave personal data in the clear for exactly the records written
earliest.

### A completed erasure in one tenant never renders another tenant's personal fields unreadable

**For tenants T and U where U is not T, a completed erasure of data subject s in tenant T leaves every
personal field of tenant U readable -- including the fields of U's own data subject whose
consumer-supplied identifier is also s.** The per-subject crypto-shred key handle is injective in the
tenant, so T's subject and U's subject are two keys, and the destruction of one is not the destruction of
the other.

This is falsifiable in one observation, and the observation is two ordinary writes and one ordinary
erasure with no race and no crash: tenant T writes a personal field for its subject `42`, tenant U writes
one for ITS subject `42`, tenant T's erasure of `42` completes, and tenant U's field must still decrypt to
its plaintext. A `null` there would be this framework stating that U's data was lawfully erased, over data
no erasure request ever named -- irreversible, because the key is gone, and silent, because a tombstone is
indistinguishable from a lawful erasure to everything downstream.

The reason the handle has to carry the tenant rather than be filtered by one is that **a key handle is the
one identity in this subsystem with no row beside it.** Every other pseudonymised identifier is a column on
a record whose query carries a tenant term; a handle is a name in a key store, and there is nowhere to put
a tenant column, so the name itself must carry the tenant. Data-subject identifiers are supplied by the
consumer from their own entities, so customer numbers, employee identifiers and e-mail addresses repeat
across tenants in ordinary multi-tenant deployments -- the collision is the common case, not the exotic one.

**How it is achieved.** One derivation, reached by both paths, refusing to produce a handle without a
tenant: `SubjectKeyHandle.ForSubjectHash` (`Erasure/SubjectKeyHandle.cs:164`) keys an HMAC over a
**length-prefixed** composition of the tenant identifier and the subject's pseudonymisation token.
Length-prefixing is load-bearing rather than stylistic: plain concatenation makes tenant `a` with subject
`bc` one input with tenant `ab` and subject `c`, which would introduce a cross-tenant collision in the act
of removing one. The result is 64 uppercase hexadecimal characters -- the same width and character set as
the handle it replaced, so no key-store object-name constraint moves, and no tenant identifier appears
anywhere it could be case-folded or rejected.

**The tenant is an explicit parameter of the derivation and is never read from ambient state inside it,
because the two callers have two different authoritative tenants.** The write path's is the ambient tenant
of the request doing the write, resolved at `CryptoShredding/FieldEncryptor.cs:111` and passed on to
`CryptoShredding/SubjectKeyManager.cs:89`. The erasure's is the tenant its own REQUEST recorded, resolved
at `Erasure/ErasureService.cs:572`; an erasure runs from a background processor whose ambient tenant is
usually nothing and is never authoritative for a request somebody else filed earlier. A derivation reading
ambient state would silently derive the wrong handle there, and a wrong handle on the erasure path destroys
nothing while reporting a completed erasure -- the mirror of the defect above, and just as silent. The
destruction is queued at `Erasure/ErasureService.cs:661`, and the destruction-confirmation pass derives the
same handle at `Erasure/ErasureService.cs:1609`.

**Every spelling of "no tenant" collapses onto one identity**, through `SubjectKeyHandle.OwningTenant`
(`Erasure/SubjectKeyHandle.cs:111`). A single-tenant write resolves the framework's default tenant
identity, while an untenanted erasure request is PERSISTED as the reserved untenanted sentinel; two
spellings would be two handles, so a single-tenant erasure would destroy a key nothing was written under.
The collapse reuses the framework's single predicate for what counts as untenanted rather than restating
it, and it happens at the caller -- the derivation itself refuses an absent tenant, which is what makes the
omission inexpressible rather than defaulted.

**The AES-GCM associated data binds the tenant transitively**, because the handle is written into it
length-prefixed. The `TenantId` field of the encryption context is populated as well, on the write at
`CryptoShredding/FieldEncryptor.cs:132` and on the read at `CryptoShredding/FieldEncryptor.cs:200`; it was
previously left unset on this path, so a field documented as preventing cross-tenant associated-data
ambiguity produced the same value for every tenant. It is **not** the control that separates two tenants --
the handle is -- and binding it without fixing the handle would have converted a silent tombstone into a
loud authentication failure while the key was still destroyed: the same unrecoverable loss, with a
different symptom. The read rebuilds it from the ENVELOPE, never from the reader's ambient tenant, because
a legitimate read happens from places with no ambient tenant at all and the associated data must be
byte-identical to what the write bound.

### A null from a field read means erased, and it is produced only when the LEDGER says so

**`IFieldEncryptor.DecryptAsync` returns `null` for a personal field only when this deployment's
append-only destruction ledger holds a row for the key HANDLE AND the key GENERATION that field's envelope
names. A row is written only after an irreversible destruction this deployment performed has already
completed, so a row's existence IS the destruction statement. The absence of a row is "not destroyed" and
the read then attempts the decrypt; an inability to READ the ledger is an exception, never "not destroyed".
An envelope that names no generation is refused before the ledger is consulted.**

The HANDLE is part of the ledger's key, not a filter applied around it, and it is the component that makes
the guarantee hold for a provider this framework did not write. **Falsifiable in one observation: record a
destruction at handle H for generation g, then ask about a different handle H' and the same g — the answer
must be "not destroyed".** Keyed on the generation alone, the oracle rests on a second invariant nothing
enforces: that one generation identifies key material uniquely across every handle. Every shipped provider
satisfies it, because each mints 128 bits from a cryptographic random source through one shared entrance —
but `KeyGeneration` constrains the SHAPE of a generation and not its ENTROPY, and says so in its own
documentation. A consumer-supplied `IKeyManagementProvider` that DERIVED its generation from the data
subject would hand two tenants' distinct keys one identifier, one row would answer for both, and one
tenant's erasure would report the other's live personal data as lawfully erased. With the handle in the key
that state is unreachable whatever a provider does, so the obligation disappears rather than being
documented somewhere a future provider author must remember to read.

**Keying on the pair keeps what the generation alone bought.** Many generations may exist at one handle, so
a destroyed generation stays destroyed whatever is provisioned at that handle afterwards, and a SECOND
destruction at one handle is a different generation and therefore a distinct row rather than a dropped one.
That last property is why keying on the HANDLE ALONE would be wrong, and the schema comments that argued
against the handle were arguing against exactly that — correctly, and not against the pair.

Achieved at `Erasure/InMemoryErasureStore.cs:405` and the two SQL stores' composite primary key, asked at
`CryptoShredding/FieldEncryptor.cs:285`, which takes the handle from `EncryptedData.KeyId` — the envelope's
own value, not one re-derived from the subject, so it names the material actually being read.

This is the guarantee that makes a `null` readable as a lawful crypto-shred, and it is falsifiable in one
observation: destroy a key through any path that writes no ledger row -- a soft-delete that leaves the
material recoverable, or a destruction performed outside this framework -- and the read must not answer
`null`. Nothing else in the system can restate it afterwards --
the tombstone reaches the consumer as a loaded aggregate with a field cleared, and nothing downstream ever
learns whether the data was erased or merely out of reach.

Absence is not the predicate, and cannot be, because the two are indistinguishable on backends with a
recovery window: Azure Key Vault answers a soft-deleted key with 404 for the whole retention period while a
single recover call restores it. Nor is the key handle, and nor is the version ordinal: a subject's handle is
derived from the subject, so the next ordinary write after an erasure provisions new material at the same
handle and the ordinal restarts from the beginning. Both then answer "not destroyed" **truthfully, about
material the reader is not holding.** The question is therefore asked of the **key generation** — an
identifier minted per provisioning from a cryptographic random source, which a later provisioning at the
same handle cannot reproduce. It is bound into the AES-GCM associated data, so a holder of the ciphertext
cannot rewrite it.

The generation is read from the **encryption context** on both the write and the read path, never from key
metadata when writing. That is what keeps the two envelope formats separable: a caller whose envelope has
nowhere to carry a generation supplies none, binds none on either side, and is unaffected — which the
encrypted audit log and the store decorators rely on. The field path's refusal of an absent generation is
what makes that conditional binding safe: without it, an envelope with the property stripped would re-read
as the no-generation form and authenticate correctly, a downgrade anyone could perform by deleting one JSON
field.

Achieved at `CryptoShredding/FieldEncryptor.cs:158` (the format-version refusal), `:176` (the
absent-generation refusal, before any key is resolved and any associated data is computed), `:172` (the
affirmative destruction predicate at `:209`, asked before a decryption is attempted because a
re-provisioned handle
fails its authentication tag rather than reporting a missing key) and `:272` (the predicate itself, which
reads the ledger and nothing else). The generation enters the associated data at
`Encryption/AesGcmEncryptionProvider.cs:592`.

The refusal when nothing can state a destruction is no longer a branch to be found. The ledger is a required
constructor dependency (`CryptoShredding/FieldEncryptor.cs:93`), so a composition with no way to answer the question fails at service
resolution rather than at a read -- and start-up refuses it before that, at
`CryptoShredding/CryptoShreddingLedgerWiringValidator.cs`.

The read path and the erasure path now agree. Erasure verification already refuses to certify a deletion a
provider cannot confirm (`Erasure/ErasureVerificationService.cs:377`); before this, erasure said "not yet
irrecoverable" while reads of the same subject said "erased", and the read is what a consumer sees.

RED-detected by `tests/unit/Excalibur.Compliance.Tests/CryptoShredding/AnAbsentKeyIsNotAnErasureTombstoneShould.cs`
— the safety arm (absent but recoverable must not tombstone), the liveness arm (a stated destruction still
degrades open), the refusal arm (no capability names what to implement), and an arm binding the trigger to
the error the real AES-GCM provider raises.

The generation half is RED-detected by
`tests/unit/Excalibur.Compliance.Tests/CryptoShredding/AnEnvelopeWithNoKeyGenerationIsRefusedShould.cs` —
the absent-generation refusal and its early-refusal assertions (the key store is never asked and no
decryption is attempted), the empty-string shape of the same absence, the format-version refusal, a liveness
arm so refusing everything cannot pass, and three arms over real AES-GCM: ciphertext written under one
generation does not open under another, does not open for a reader that omits the generation, and a caller
supplying none on either path still round-trips. The re-mint case itself — an erased subject's earlier record
still tombstoning after a later write re-mints the subject's key — is covered by
`CryptoShredding/CryptoShredTwoSubjectShould.cs`, which also covers the full real-stack path.

Providers are held to the generation-scoped contract by
`KeyManagementProviderConformanceTestKit`, whose load-bearing arm asks about a generation the backend has
never held **at a live key handle** and requires "destroyed" — a provider that answers the handle instead of
the generation fails it.

**Consumer obligation:** a host whose key provider does not implement `IKeyDestructionStatusProvider`
cannot load an erased subject's aggregate — degrade-open does not apply to it. The exception names the
capability. This is the deliberate direction: a failed read is loud and recoverable, a fabricated erasure
tombstone is neither.

**Known gaps:**

- **A provider that supplies no generation cannot be used for crypto-shredded fields.** `KeyMetadata.Generation`
  is nullable, so such a provider compiles and every other capability works; its field writes produce
  envelopes the read path then refuses. The refusal names the cause. Nothing establishes at startup that a
  configured provider supplies one.
- **A provider must destroy whatever records the generation when it destroys the key.** Where the generation
  lives in a sidecar record rather than being intrinsic to the backend's own versioning, that record dying
  with the key is the provider's obligation, and a provider that leaks it hands the next provisioning at the
  same handle the destroyed generation's identifier. The shipped providers do this; a consumer-authored one
  is not checked.
- **The generation mapping for Azure Key Vault is unverified by us.** There is no conformance suite for that
  provider, so its use of the opaque key version as the generation is reasoned from the backend's documented
  versioning rather than measured.
- **Ciphertext written before the envelope carried a generation cannot be read.** It is refused by format
  version rather than mis-read, and there is no read path for it: the generation it would need was never
  written down.

### Encrypting a store does not change what that store can do

**Wrapping an outbox store for encryption preserves the capabilities the store already had. A capability
that reports on the store's own shape is answered by the store underneath, never by the wrapper.**

The one that matters in practice is whether the store retains a successfully-sent message as a countable
row or deletes it on send. The relational delete-on-sent stores report that they do not; the tracking
stores report that they do. A wrapper that failed to pass the question through would make a delete-on-sent
store indistinguishable from a tracking one for exactly the consumers who enabled encryption, and callers
— including the conformance kit — would then go looking for a row that store had already deleted.

A capability is passed through unmediated only when no member of it, declared or inherited, carries
message content: those members move identifiers, reasons, sequence numbers and fencing tokens. A
capability whose surface *can* carry a payload is resolved to a mediating view instead, so that reaching
it cannot become a way around the encryption — and one that is neither is denied rather than guessed at.
The forwardable set is enumerated by name at
`Encryption/Decorators/EncryptingOutboxStoreDecorator.cs:56` and supplied to the base decorator at `:92`.

### Retention enforcement acts only on the scope the host declares

**The retention policies handed to a retention contributor are exactly those of the types the host
declared, through `AddRetentionPolicies<T>()` or `AddRetentionPoliciesFromAssembly(assembly)`. A type the
host did not declare is never in that set, whether or not it is annotated with `[PersonalData]` and
whether or not its assembly is loaded.**

The population is computed from the named type or assembly when it is declared
(`Retention/RetentionPolicyDeclaration.cs:23`, `:40`) and the enforcement service acts on their union and
nothing else (`Retention/RetentionEnforcementService.cs:55`). It is never discovered by scanning the
process's loaded assemblies. Enforcement deletes, so a spurious member of an ambient population is a
deletion nobody asked for, in a scope nobody can enumerate.

- **In scope:** every `[PersonalData]` public instance property with a positive `RetentionDays` on a
  declared type. Declaring a type or assembly that yields no such property is refused at registration.
- **Never deleted by a policy:** data of any undeclared type; data inside its retention period as of the
  pass's evaluation time (`RetentionContributorContext.AsOf`, read from the registered `TimeProvider`);
  anything at all on a dry run.
- **Not silently switched off:** with enforcement enabled and nothing declared, host startup fails
  (`Retention/RetentionEnforcementOptionsValidator.cs:38`). The one exception is a host whose only
  contributors are the built-in outbox and inbox ones, which delete by their own age bound and never read
  the declared policies. Enforcement is off only when the host sets `Enabled = false`.
- **Legal holds are not consulted by the enforcement pass.** A retention policy names a type and a
  period, not a data subject, so the pass has nothing to check a hold against. A contributor that deletes
  subject data must check `ILegalHoldService` itself before deleting (see consumer obligations).
- **Registering retention enforcement is the only way the pass starts.** No other registration in this
  package starts it.

### A declared aggregate retention survives an erasure whole, readable, and named on the record

**Guarantee: an aggregate type declared through `AddErasureRetention` is not
tombstoned, its snapshots are not deleted, its read-model rows are not cleared, its `[PersonalData]`
fields **written while the declaration was in force** still DECRYPT after the subject's key is
destroyed, and the erasure record names the type with
the lawful basis, the written justification, and the period it is kept under. This holds for EVERY
data subject named inside a retained type, not only the one the obligation was written with in mind. An aggregate
type that is NOT named is erased exactly as it is with no retention declared at all.**

Note the word: this is the opposite direction from the retention *enforcement* pass above, which
DELETES after a period. This one WITHHOLDS destruction because the law requires the data kept — the
Article 17(3) case, where the obligation covers the subject's identity and not merely the transaction.

**The unit is the aggregate type, WHOLE, and that is a legal fact rather than a simplification.** An
obligation to keep a record attaches to the RECORD: a statute requiring sales records to be kept does
not require the buyer and permit deleting the salesperson, because a partly-erased record is a mutated
record and a mutated record has no evidentiary value — which was the entire reason for keeping it. So a
retained type is never tombstoned, for anyone, and every subject named inside it keeps their key for it.
A subject who appears in a retained aggregate is not erased from it, and is told so on the record.

**How it is achieved.** It is implementable at this unit because an event belongs to exactly one
aggregate: an aggregate boundary is therefore an event boundary, and a retention can be honoured
without ever splitting a stored row, whose erasure is total — all fields and its type name together.
One branch in the event-store contributor's per-reference loop skips the whole destructive triple —
snapshot delete, tombstone, read-model clear — for a declared type
(`Erasure/EventStoreErasureContributor.cs:187` in `Excalibur.EventSourcing`). The event store is unchanged and knows nothing about retention; it is told
which aggregate to erase and has no opinion about which ones it is asked for.

Readability is what makes the surviving record worth keeping, and it comes from the key handle rather
than from new cryptography. A key handle is a NAME, and the provider mints random material at whatever
name it is given, so widening the handle with the aggregate type yields a second, independently
destroyable key with no envelope machinery (`CryptoShredding/SubjectKeyManager.cs:109`). Only a DECLARED
type's handle is widened: every other value stays under the subject's own handle, which is the one the
erasure destroys unconditionally and the only one it can enumerate. The erasure additionally refuses to
destroy a discovered key that resolves to a retained handle (`Erasure/ErasureService.cs:628`). The
decrypt path needs no change, because the envelope carries the handle that produced it.

What was kept reaches the signed certificate beside the framework's own store-kind exemptions
(`Erasure/ErasureService.cs:720`). This is the part that makes the behaviour detectable from outside: a
certificate reporting a clean completion over data deliberately kept cannot be told from one over data
destroyed, and nothing downstream ever learns which it was.

**KNOWN GAP -- UNVERIFIED: the entry does NOT carry a subject-scoped statement, and an earlier version
of this document asserted that it did.** The entry carries the basis, the declared justification, the
period and the retained key handle (`ErasureException`). It carries no member and generates no text
saying that the REQUESTING subject's own personal data is what persists in the named type. The declared
justification is written about the RECORD, so on its own it does not tell the person reading it that
THEY are what survives inside it. Whether a subject-scoped notice is owed, and what it must say, is a
requirements question and is not settled here.

*Superseded wording, quoted so a reader who inherited it recognises it: "The entry states, in its own
words, that the requesting subject's personal data was not erased and lawfully persists in the named
type." That was false when written -- no such statement is produced.*

**Expiry is declared and reported, never promised — and releasing it is not yet a supported operation.**
The period is required, so it cannot be left unsaid, and startup validation rejects zero and below; an
absurdly long period is still constructable and is the declaring party's statement to defend. The instant
a particular record's obligation lapses depends on facts the framework does not hold (the transaction
date, the jurisdiction, whether the period was extended), so acting on someone else's statutory clock is
not a promise it can keep.

**The exit is NAMED ON THE RECORD, and that is what makes the period actionable.** Removing a declaration
does not destroy anything it protected: the retained key is not the subject's handle, so nothing in the
erasure path ever queues it, and a completed request is terminal and will not run again. The retained
record therefore survives until someone destroys that key BY NAME — so the erasure record names it. Each
retention entry carries `ErasureException.RetainedKeyHandle`, the handle still protecting THIS data
subject inside THAT aggregate type, in the form `IKeyManagementAdmin.DeleteKeyAsync` accepts. When the
statutory period ends, destroying it is the whole of the release.

It is written on the SAME pass that decides which handles to spare (`Erasure/ErasureService.cs:600`), from
the same map, so the set the erasure excluded and the set the record names are one iteration and cannot
disagree. It discloses nothing new: the handle is composed from the data-subject hash the payload already
states and a digest of the aggregate type the entry already names.

An entry with a NULL handle is a signal rather than a blank — a contributor spared a type this erasure made
no key decision about. It is left absent rather than invented, because a fabricated handle would be
destroyed with confidence.

**Evidence for the release path.** `TheRecordNamesTheHandleThatReleasesARetentionShould` (compliance unit
suite) binds it by USING the handle, not by matching a string: one arm asserts the recorded handle is
byte-for-byte the one the real write path put on a real envelope, and one destroys the recorded handle
through the real key admin and shows the retained data becomes unrecoverable. A third proves an entry the
erasure made no key decision about carries no handle. A plausible-but-wrong handle reddens all three.

**Evidence.** `ARetainedAggregateSurvivesErasureShould` (event-sourcing unit suite) binds the skip, the
per-type reporting, the subject-facing disclosure on the entry, the tenant scope, the read-model half,
and — as its non-vacuity controls — that an undeclared type and a host with no declarations are erased
unchanged. `ARetainedAggregateTypeKeepsItsOwnKeyShould` (compliance unit suite) binds the key half
against a real in-memory key provider and real AES-GCM: destroying the subject's handle leaves a
declared type's fields decryptable and an undeclared type's unrecoverable, a SECOND data subject named
in the same retained type stays readable through their own erasure, and a declaration belonging to
another tenant protects nobody here.

**Known gaps.**

- **Declaring a retention protects only what is written afterwards.** Events written before the
  declaration were encrypted under the subject's shared handle, which the erasure destroys, so they
  survive the tombstone with their personal fields no longer decryptable. Re-encrypting them is a
  migration the framework does not perform. **The signed certificate does not assert otherwise:** a
  retention entry states that the erasure did not TOMBSTONE the record, names the obligation it is kept under, and
  says nothing about whether this data subject's fields inside it are still readable, because the pass
  that writes the entry cannot establish that. Read the entry as *this record was kept*, never as *your
  data in it is intact*.
- **A contributor constructed with no retention registry honours no retention.** The constructor takes
  it explicitly, so a host that passes `null` tombstones everything the mapping returns. There is no
  overload that omits it silently.
- **Coverage is judged per store kind, not per aggregate type.** A discovered personal-data location in
  the event store counts as covered because a contributor declares that store kind, whether or not the
  particular aggregate behind it was retained.
- **Verification class.** This guarantee is established by example-based tests. The seam's blast radius
  is catastrophic; property-based and model-checked rungs are UNVERIFIED for it.

### An erasure completion certificate — what it asserts, and what it does not

**Guarantee: a COMPLETION certificate exists only for a request that reached `Completed`, and `Completed`
is reachable only when every one of the following held. Any one of them unmet records an error, and the
branch that issues a completion certificate is the only branch that does not run while an error exists.**

> **Amended.** This previously read that *the* certificate-issuing branch is the only branch that does
> not run while an error exists. That is still true of a **completion** certificate and is no longer true
> of certificates generally: a request that reached `PartiallyCompleted` now also receives one, and it is
> issued from a branch where errors exist by definition. The two are different documents making different
> claims, and the sentence above was correct only while the second did not exist.

- A data-inventory discovery source is registered, or the host explicitly opted into
  `ErasureOptions.KeyShredOnlyErasure`. Coverage that was never looked for is not coverage.
- At least one data location is registered. An empty registry is an absence of evidence, so a certificate
  is not issued over one (`Erasure/ErasureService.cs:790`).
- Every registered data location was reported erased by a named contributor. A contributor that reports
  success without naming the table-and-field pairs it erased discharges nothing.
- No discovered store kind was left uncovered by a key destruction, a contributor or a declared exemption.
- The `[PersonalData]` annotation scan completed. A scan that could not run leaves the annotated set a
  lower bound, and an unknown category is indistinguishable from a covered one.
- Every key destruction the framework attempted was reported irrecoverable *now* by the key-management
  provider. A key merely scheduled for destruction is still recoverable and records an error.

**Guarantee: a request that reached `PartiallyCompleted` also receives a certificate, and that certificate
records what the erasure did NOT reach, distinctly from what it lawfully retained.**

An erasure that partly succeeded used to receive nothing: the call refused, and because the request is
terminal it could never be produced afterwards. What the consumer was left with is the status row's error
text — unsigned, unstructured, mutable, with no retention and no canonical form. Refusing the document
does not make the failure legible; it destroys the only durable signed record that the work which DID
happen happened.

The certificate for a partial carries a list of unreached locations, and each entry states four things:
the kind of store still holding the data, the mechanism by which the erasure did not reach it, an
**explicit** statement that no lawful basis is claimed for that retention, and the controller's remaining
obligation together with its operational cost. That list is a sibling of the Article 17(3) exemptions and
nothing appears in both — an exemption carries a legal basis, and presenting an unmet obligation as one
would turn a failure into a defensible retention.

Two properties of the surrounding behaviour are part of this guarantee, because without them it is
unsafe:

- **A partial certificate is the one written on the execution path, never a reconstruction.** The
  persisted status keeps counts and a single joined error string, so a certificate rebuilt later cannot
  name which store kind went unreached — it would attest an incomplete erasure while being silent about
  the incomplete part. Reconstruction of a partial is refused.
- **Failing to write the certificate cannot fail the erasure.** Writing it signs a payload and resolves a
  store, so it can throw; that throw is caught and the erasure still reports the keys it destroyed. An
  erasure that destroyed a key and then reported as never having happened would leave a consumer unable
  either to redo it — the key is gone and the request is terminal — or to prove it was done. The
  erasure's outcome is decided by what the erasure did, never by whether the document about it was
  written.

**Evidence.** `APartlySucceededErasureStillProducesEvidenceShould` — four arms: a certificate is produced
at all; it names the store not reached; it claims no lawful basis and the unreached store never appears
among the exemptions; and the keys destroyed are still reported when the certificate write throws. Each
is RED-detecting: removing the execution-path write reddens the first three, and removing the fail-open
catch reddens the fourth.

**Consumer obligation.** The remedy named on the certificate takes the affected read model offline for
its duration and must not be run against a live projection processor — a rebuild interrupted part-way
leaves the subject cleared from some rows and not others with no record of which.

**Known gap, and the rung.** This makes the EVIDENCE honest. It does not make the erasure reach further:
erasure still does not propagate to projections, and it remains whole-aggregate. This seam's blast radius
is catastrophic and its rigor floor is R4; **R3 and R4 are UNVERIFIED** — the arms above are
sampling-class and say nothing about the state space.

**Guarantee: `Verification.Verified` is `true` only when the framework itself established the destruction
the certificate attests — every identifier in `Verification.DeletedKeyIds` was reported irrecoverable by
the key-management provider. `false` means *not established*, for any reason.** It is not a claim that the
erasure failed: `false` is the ordinary value for an erasure discharged entirely by record deletion, where
the framework recorded what its contributors reported and verified nothing itself. `Verified` and
`Verification.Methods` are derived from one value in `PersistCompletionCertificateAsync`, so a certificate
reading `Verified = true, Methods = None` — verified by nothing — is inexpressible rather than unlikely.

**Guarantee: a completion is recorded only over a request that is still IN the run, so a legal hold or a
cancellation recorded while the contributors were working is never overwritten by a signed `Completed`.**
Holds are re-checked once, before a contributor pass that is long by design, so the window between that
check and the completion write is real. The store closes it rather than the service: `RecordCompletionAsync`
writes only where the status is one of the permitted source states, and refuses otherwise. The permitted
set is stated positively, so a status added to the enum later refuses rather than inheriting permission.
A refused completion attaches no certificate to the request, and a certificate is issued only for a
request that reached `Completed`.

**Guarantee: coverage is judged against the data locations REGISTERED with the data inventory, never
against the annotations on a type.** `[PersonalData]` classifies a property; it does not say which table
and field hold it, so it cannot become a data location. It drives a separate arm — an annotated category
the inventory never located blocks completion — and registration remains an explicit act
(`IDataInventoryService.RegisterDataLocationAsync`). The obligation set is the whole tenant registry, read
once per erasure (`Erasure/DataInventoryService.cs:101`), never a subject-scoped slice.

**Guarantee: a host with registered projections cannot reach `Completed`, because the framework cannot
erase a read model and will not report that it did.** Erasure tombstones event rows in place and notifies
nothing, so a projection that already folded a subject's events keeps them. That is a real gap and it is
not closed by this guarantee — what this guarantee closes is the gap being reported as success.

The mechanism reuses the fail-closed path rather than adding one: an erasure contributor covering NO
store kind reports failure whenever persisted projections are registered, a contributor failure is an
error, and `Completed` requires zero errors. It stands down in the three cases where reporting a gap
would be false — no projections registered; only EPHEMERAL projections, which are computed on demand and
persist nothing, so a tombstoned stream already yields a clean result; and a consumer who has registered
their own contributor declaring `DataStoreKind.Projection`, who has taken the responsibility on.

**Why this needed saying at all, and it is the coverage guarantee above seen from its blind side.**
Coverage is judged against locations the CONSUMER registered. Nothing registers a projection store as a
location, and nothing can — the framework cannot enumerate a consumer's read models. So the coverage gate
had nothing to fail on, and a certificate over untouched read models was not merely possible, it was the
default. **A gate that is conservative over the inventory it is given is not conservative about an
omission from that inventory.**

**Consumer obligation.** Clearing a subject from a read model is yours to drive. For a projection keyed
per aggregate, `IProjectionRecovery.ReapplyAsync` for the subject's aggregate rewrites that row from the
tombstoned stream; for rows the subject shares with others, rebuild the projection. Neither is reached by
the erasure path.

**And a caveat on the first of those, because recommending a call without its known defect is how a
guarantee document misleads.** For a FULLY erased aggregate the replay yields no position -- every event
is a tombstone -- so the write falls to the unconditional surface and leaves the row with no established
position. The compliance outcome is achieved: the subject's data is gone from that row. What follows is
that the next batch finds a row with no position to advance from and is REFUSED, so the projection stops
advancing until it is rebuilt. **Use the call for erasure, then rebuild that projection** — the refusal is
loud and recoverable, but it does not clear itself, and nothing in the framework rebuilds the row for you.
The event-sourcing guarantee document carries the full mechanism.

**Guarantee: an EMPTY registry and an UNREADABLE registry are different facts and are answered
differently, because only one of them is a misconfiguration.** An empty registry does not fail host start —
a clean install is empty by definition, and refusing to boot would deadlock the host that must start in
order to register — it is logged at startup and refuses certificate *issuance*. A registry that cannot be
read fails host start (`Erasure/DependencyInjection/ErasureDiscoverySourceValidator.cs:128`) and, if it
becomes unreadable later, surfaces the read failure at execution rather than reporting that nothing is
registered.
### Erasure certificate integrity — what a signature on a certificate proves

**Guarantee: a certificate's signature covers every claim the certificate makes, not a selection of them.
Two certificates that disagree about anything — what was destroyed, whether anything was verified, which
exemptions were claimed and on what legal basis, when the document was generated, which format version it
declares — cannot share a signature. Verification either establishes that the payload presented is
byte-for-byte the payload that was signed, or it establishes nothing; there is no third answer and no
tolerant mode.**

**What it does NOT guarantee.** That the claims were true when they were made. A certificate can be
faithfully signed and still attest to a verification nobody performed. Integrity and truthfulness are
different properties, and a signature can only carry the first. The section above records separately what
a generated attestation may be read to assert.

**How it is achieved.** The claims live on `ErasureCertificatePayload`, a type with no signature member;
`ErasureCertificate` is the envelope carrying that payload plus the tag. The signed input is a canonical
serialization of the payload *whole* — `ErasureCertificateCanonicalizer`, over a source-generated context
whose every byte-affecting option is pinned rather than inherited. Nothing enumerates fields, so a claim
added to the payload is covered because there is nowhere else for a claim to live. The format version sits
*inside* the payload and is therefore signed: a version marker beside the signature would let whoever holds
the document choose which scheme a verifier applies to it, which is a downgrade oracle. The tag itself
carries its scheme (`v2:{mac}`), so a verifier selects a canonical form without first parsing a payload it
has not authenticated, and refuses any scheme it does not implement rather than falling back to an older
one. `ErasureCertificateVerifier.Verify` recomputes over the same canonical form and compares with
`CryptographicOperations.FixedTimeEquals`. Issuing a certificate with no signing key configured is refused:
an unkeyed hash is recomputable by anyone and is not a signature.

**Storage is part of the guarantee, and it is where it was previously lost.** Signing the payload whole
makes a demand of a store that did not exist while the signature covered three identity fields: a stored
certificate must come back byte-for-byte, or its tag can never be recomputed. Reconstructing the document
from a column per claim does not satisfy that, because a column has a type and a type may be unable to
hold the value it is given — measured on Postgres, `TIMESTAMPTZ` keeps microseconds where a .NET
`DateTimeOffset` carries hundreds of nanoseconds, so three of a certificate's instants came back rounded
and the document no longer matched its own signature. The Postgres store therefore persists the canonical
form verbatim and restores it whole; its remaining columns are indexes for querying and nothing reads them
back into the document. The SQL Server store's `DATETIMEOFFSET` is lossless for this type and it continues
to project into columns; the conformance arm below is what establishes that, for each store, against a
real engine rather than by argument.

**Why a false "altered" would be worse than no verifier.** On a compliance record, a report that the
document was interfered with is a finding a consumer may be obliged to escalate or investigate. A verifier
shipped against a store that cannot round-trip a payload would produce that report on correct data — and
the honest reading is not that the alarm is false but that *we* are the party altering the document. That
is why the tolerance a team reaches for first is refused: a verifier lenient about the fields we know we
drop is a verifier that certifies our own alteration.

### SOC 2 attestation — what a generated report may be read to assert

**Guarantee: a generated report attests only to criteria that a registered validator actually assessed.
Every other criterion is reported as not assessed, is excluded from the compliance percentage — from the
numerator and the denominator both — and does not move the auditor opinion.**

This section previously read `UNVERIFIED` in its stronger sense: the behaviour had been *measured to
contradict* the target. That measurement is no longer current. The value set can now hold the fact, the
aggregation honours it, and two arms bind the one-token mutant that would undo it.

**How it is achieved.** `CriterionOutcome` carries three states — `NotAssessed`, `Met`, `NotMet` — and
`ControlSection.Outcome` is typed by it (`Soc2Report.cs`), replacing a `bool` in which "nobody assessed
this" and "we assessed it and it failed" were the same value. `Soc2ReportGenerator` derives the state from
whether any validation result exists at all, rather than from whether the results were favourable; the
compliance percentage is computed over assessed sections only; the exception loop takes `NotMet` sections
and skips both `NoExceptions` and `NotTested`, so a criterion nobody examined raises no finding against
the consumer; and an unassessed criterion instead receives an entry whose text attributes the gap to this
framework's coverage rather than to the consumer's controls. Where nothing at all was assessed the report
is `Unknown`, not `NonCompliant` — the latter reached an adverse opinion for a host that had simply
registered no validators.

**The enforcing arms, and the one-token mutant they bind.** The violating mutant named below was applied
and measured, not predicted: collapsing the three-state derivation back to `validationResults.Count > 0
&& …` — which maps the not-assessed state onto the failing one — turns
`Soc2ReportGeneratorShould.Report_an_unassessed_criterion_as_NotAssessed_rather_than_NotMet` and
`Soc2ReportGeneratorDepthShould.Section_is_not_assessed_rather_than_not_met_when_no_validator_is_registered`
RED, and only those. Both directions of the biconditional are covered: a criterion with no registered
validator is reported not-assessed, and a criterion whose validator ran and failed is still reported
not-met.

**Consumer obligations.** Validators are opt-in. A report generated with none registered for a criterion
is not a deficient report — it is a report that says, in the document handed to an assessor, that this
framework did not assess that criterion. Substantiating it is the consumer's to arrange. Because that
state is legitimate, it is never a startup failure: a host whose enabled categories include one that no
registered validator can assess starts normally and logs a warning naming the category and the
registration that would cover it. The signal reaches the person who wrote the configuration, and the
report remains the contract.

**Closed gap, recorded because this section previously bounded the guarantee on it.** This paragraph
used to read that one layer below the criterion layer a control validator reported effectiveness as a
`bool`, so a validator whose own check could not run — it threw, or had nothing in scope — had no way to
say so and had to spell the outcome as either effective or deficient. That is no longer true, and the exit
condition stated here at the time — *until that type carries the third state too* — has been met.

**Guarantee: a control validator reports a verdict only about a control it actually examined. A control
outside its declared `SupportedControls` is reported as not verified — never as deficient, which would
reach an assessor as a finding against the consumer that this framework never made.**

**How it is achieved.** `ControlOutcome` carries three states — `Deficient`, `NotVerified`, `Effective` —
ordered so a proven deficiency can never rank above a control nobody examined, because a finding is
knowledge and an absence of examination is not. Each shipped validator's unknown-control branch passes the
unverified band explicitly rather than relying on the default score, which was zero and therefore mapped to
the worst available verdict: omitting one optional argument used to state *examined, mechanism absent,
failing* about work that never happened.

**What binds it.** `EveryControlValidatorReportsNotVerifiedForAnUnsupportedControlShould` asserts the
property over every shipped validator, one case each, and guards its own premise by first asserting the
validator does not declare the probe identifier — so a case cannot silently stop being about an unsupported
control. `ControlValidatorConformanceKitLivenessShould` proves the shipped conformance kit's own arm can
REJECT a validator answering `Deficient` or `Effective` and ACCEPTS one answering `NotVerified`; restoring
the pre-fix predicate reddens the deficient case and no other.

**Closed gap, recorded because this section previously bounded the guarantee on it.** The guarantee now
covers both operations. `RunTestAsync` was bound only against fabricating a *pass* — its conformance arm
rejected `NoExceptions` for an unsupported control and permitted every other outcome, so a validator could
report a control it never examined as tested-and-failed: the same unearned accusation in the sampling
operation's vocabulary, reaching the generated report through the same path. That arm now requires
`NotTested`, matching its `ValidateAsync` sibling's single-value shape rather than excluding one value and
permitting the rest — which matters because the accusation is reachable through more than one member of
`TestOutcome`, so a blocklist would have left it expressible.

**The enforcing arms.** `ControlValidatorConformanceKitLivenessShould` proves the tightened arm REJECTS a
validator answering `ControlFailure` (nothing examined, exceptions asserted) and one answering
`SignificantExceptions`, and ACCEPTS one answering `NotTested`. Restoring the pre-fix predicate reddens
exactly those two cases and no others. The framework's own base is already conforming:
`BaseControlValidator.RunTestAsync` returns `NotTested` unconditionally, because the control verdict
travels on `ControlSection.ValidationResults` and this field is therefore free to report only what the
*test* did.

### An erasure whose key the provider has only scheduled is never Completed, and is never stuck

**SAFETY: a request is `Completed`, and has a completion certificate, only once every key it destroys has
been confirmed irrecoverable by the key-management provider.** A key the provider has scheduled for
destruction is still recoverable (AWS KMS `CancelKeyDeletion`, Azure Key Vault recover), and the execution
step registers it as an error (`ErasureService.ExecuteKeyDeletionsAsync`, the `ScheduledIrreversible` case),
so `Completed` is unreachable at execution while one exists.

**LIVENESS: a request whose only outstanding work is a scheduled key is `AwaitingKeyDestruction`, not
`PartiallyCompleted`, and reaches `Completed` on the first completion pass after the provider destroys the
key.** `ErasureService.ExecuteAsync` enters that state only when every error is a scheduled key; any other
error keeps the existing `PartiallyCompleted` / `Failed` outcome. `IErasureCompletionProcessor`
(`ErasureCompletionProcessor`) lists every waiting request and calls `ErasureService.ConfirmKeyDestructionAsync`,
which asks `IErasureVerificationService.VerifyKeyDeletionAsync` about the subject key and every key the
inventory associates with the subject, and completes and certifies the request through the same certificate
path an immediate erasure uses only if every answer is "destroyed". It never deletes, schedules or
re-executes. The optional scheduler calls the processor each cycle; a host with no background service calls
it itself. **Liveness is conditional on something calling it** and on an `IErasureVerificationService`
being registered; without one, waiting requests are logged and stay waiting.

**The provider is the authority on destruction; the reported instant is not.** The time the provider gave
when it scheduled the key is written to the request's message for operators and is never read by
confirmation. `VerifyKeyDeletionAsync` confirms a key only through the provider's
`IKeyDestructionStatusProvider`, and a provider that does not implement it is never confirmed: the request
stays `AwaitingKeyDestruction`, and a Warning names the provider at startup and again on first use. There is
no fallback to the key lookup, because a lookup that reports a recoverable key as absent cannot confirm
anything. Azure Key Vault is such a lookup: a soft-deleted key answers 404. Every shipped provider implements
the capability: in-memory and Vault Transit have no recovery window, so a key they do not hold is destroyed. The
Key Vault provider answers from its deleted-keys collection, read on both sides of the live read so a
recovery between reads cannot make a key look destroyed. The AWS KMS provider answers from every alias of the
key, the per-version aliases included, because rotation leaves each superseded CMK enabled; and its deletion
schedules every version, not only the current one. The multi-region provider answers "destroyed" only when
every region does.

**One certificate per request, even with several confirmers.** A certificate issued at confirmation has an id
derived from the request id (`ScheduledKeyDestructions.ConfirmationCertificateId`), so the certificate insert
is the claim: a second confirmer's insert is refused as a duplicate and it records completion against the
existing certificate. The scheduler never resets a request to `Scheduled` when it is `InProgress`,
`AwaitingKeyDestruction`, `Completed` or `Cancelled`, because it cannot tell its own failure from another
instance's ownership.

**When a request reaches `Completed` depends on the provider and its configuration:** at once on the
in-memory and Vault providers and for AWS KMS imported key material; after AWS's pending-deletion window
(7 to 30 days) for a KMS-generated key; at once on Azure Key Vault when purge protection is off and the
credential may purge, because the provider purges the soft-deleted key when asked for immediate destruction;
otherwise after the vault's retention period. A refused purge degrades to `ScheduledIrreversible`, never to
`Completed` and never to an exception.

Locked by `ErasureAwaitingKeyDestructionShould` (enters the state; no certificate while waiting; stays
waiting while the provider holds the key; a past reported instant is not proof; completes on confirmation;
a genuine failure stays on the failure path; not re-executed; not cancellable; no verifier leaves it waiting),
`AwsKmsErasureReachesTerminalShould`, `AzureKeyVaultErasureReachesTerminalShould` (purge accepted completes;
purge refused falls back; soft-deleted is not destroyed; purged completes),
`MultiRegionKeyDestructionReachesEveryRegionShould`, `ErasureCompletionConcurrencyShould` (one certificate
under racing confirmers; no reset of an owned or finished request), and the store conformance arm
`AwaitingKeyDestruction_ShouldBeListable_NotRescheduled_AndNotCancellable`. Each provider's own answer is
locked beside it: `InMemoryKeyManagementProviderShould` and the `IsKeyDestroyedAsync_*` arms of
`VaultKeyProviderShould` (a held key is not destroyed; an absent key is; a vault that cannot be asked throws
rather than answering; the read bypasses the metadata cache).

## How it is achieved

- **One derivation point per store.** Each store exposes a single private ambient-scope property, and
  every tenant-facing statement reads it. Nothing binds a tenant by hand, so there is no per-call-site
  opportunity to bind a nullable value and produce a predicate that silently matches nothing.
- **Deployment mode decides the shape**, and it is read from the multi-tenancy configuration rather than
  inferred from whether a tenant context is registered — the framework always registers a single-tenant
  default, so presence would make every deployment look multi-tenant and strand existing rows.
- **The capability marker cannot be separated from the dependency.** Stores register through the
  tenant-scoped registration seam, which resolves the tenant context itself and hands it to the factory.
  A store built without it is inexpressible through that seam, so a store cannot carry a truthful-looking
  isolation marker while never having received the dependency.
- **The startup check fails closed.** Both contracts are in the set the multi-tenancy registration
  verifies, so a multi-tenant host registering an unscoped implementation fails at startup rather than
  leaking at runtime.

### Failure reporting is discriminable

**A caller can tell "this erasure request is already stored" from every other reason a save can fail, by
exception type alone, and without referencing a database provider.**

`SaveRequestAsync` raises `DuplicateErasureRequestException` when — and only when — a request with that
identifier is already stored; `SaveCertificateAsync` raises `DuplicateErasureCertificateException` on the
same terms. Every other terminating condition carries its own type, and the one most easily mistaken for a
duplicate sits outside the `InvalidOperationException` hierarchy altogether:
`ErasureStoreNotProvisionedException` for a schema that is absent or missing columns the store binds,
`TenantRequiredException` for an unresolved ambient tenant, `ObjectDisposedException` for a disposed store.

The distinction is load-bearing, not cosmetic. The readings demand opposite responses: "already stored"
means the request is safe and the caller should stop, while every other condition means nothing was stored
and the caller must re-file. A caller that cannot tell them apart, and takes the first reading, silently
discards erasure requests — and an erasure request is a data subject's exercise of a statutory right, so
nothing downstream reports the loss.

`GetStatusAsync` and `UpdateStatusAsync` stay total over lookup: a request that is not there is reported
as `null` and `false` respectively, never as an exception. The only condition under which they do not
return is a store whose schema cannot answer at all.

### Schema provisioning is settled at startup, not on the write path

**A host whose erasure schema is absent or stale fails to start, rather than failing one erasure request
at a time.** Each provider store contributes an `IErasureSchemaValidator`, and the hosted service
registered alongside it verifies every one during startup. Provisioning is a property of the deployment,
so it is checked once, where the fault is attributable — not on the path of a data subject's request,
where it is not.

A first-use check remains inside each store as the fail-closed floor for consumers that never run that
hosted service: a store constructed directly, or a serverless host with no startup pipeline. It raises the
same provisioning type, so the floor and the startup check report the same condition the same way.

## Consumer obligations

- **Set the ambient tenant; do not rely on the argument.** Scope comes from the ambient tenant context.
  The `tenantId` parameter can narrow a result but cannot select a tenant.
- **Always supply a tenant when checking legal holds in a multi-tenant deployment.** Omitting it widens
  the check to estate-wide holds, which is safe but broader than you probably intend, and is reported at
  warning level when it changes the outcome.
- **Do not scope the background sweeps.** Expiry of holds and draining of scheduled erasure requests are
  estate-wide by design. Scoping them to one tenant would stall erasure for every other tenant and make
  expired holds permanent.
- **Single-tenant deployments need no action for the row-level tenant term.** It applies only when
  multi-tenancy is configured; existing rows are untouched and no migration is required.
- **Record a consumer-performed destruction under the handle your envelopes NAME.**
  `IKeyDestructionLedger.RecordDestroyedGenerationAsync` requires the handle as well as the generation, and
  the read asks about the handle the envelope carries. A destruction recorded under a different spelling of
  the handle leaves those reads FAILING rather than reporting the erasure — the safe direction, and not the
  one you intended. There is no way to record a destruction that answers for every handle, deliberately:
  that is the shape that would let one key's destruction tombstone another's data.
- **A custom `IKeyManagementProvider` MUST mint its generations from a cryptographic random source**, never
  derive them from the handle, the data subject, a counter or a clock. `KeyGeneration.Mint` is the supported
  entrance. The ledger's composite key means a derived generation no longer causes a false erasure claim
  across handles, but a generation reused at ONE handle still would: it is how a re-provisioned key is told
  from the material that was destroyed.
- **An erasure request must name the same tenant the data was written under.** The per-subject
  crypto-shred key handle carries the tenant as part of the key's identity, so an erasure filed for tenant
  T destroys only T's key. In a multi-tenant deployment with `RequireTenant` enabled, the store records the
  ambient tenant of the call that filed the request, so establishing the tenant scope is sufficient and
  nothing further is required. With `RequireTenant` disabled, the store records the `TenantId` the request
  carries, and a request naming a different tenant from the one whose ambient scope wrote the data will
  destroy nothing while reporting a completed erasure. A request naming no tenant at all is treated as the
  single-tenant default identity, which is the identity a single-tenant write resolves.
- **Catch `DuplicateErasureRequestException`, never `InvalidOperationException`, to detect a re-filed
  request.** The base type is also raised by conditions meaning the request was *not* stored, so a caller
  branching on it treats an unprovisioned database as a request already on file and drops it.
- **Declare the retention scope explicitly.** Call `AddRetentionPolicies<T>()` for each type whose
  retention you want enforced (or `AddRetentionPoliciesFromAssembly` for a whole assembly). A contributor
  must delete only data of the types in `RetentionContributorContext.Policies`, and only records older than
  the policy's `RetentionDays` as of `AsOf`.
- **Check legal holds in your retention contributor.** The enforcement pass does not; a contributor that
  deletes a held subject's data deletes it.
- **Check a certificate's signature with `ErasureCertificateVerifier.Verify`, and treat its three outcomes
  as three different findings.** `Verified` means the claims are as signed. `SignatureMismatch` means the
  document was altered after issue — investigate the custody of the store it came from. `NotVerifiable`
  means the certificate carries no signature this framework can check, which is **not** evidence of
  tampering and must not be escalated as one: its claims were never covered, no later check can establish
  them, and re-issuing is the only remedy. A certificate issued under an earlier scheme reports
  `NotVerifiable`.
- **Do not read the certificate's version field to decide how to verify it.** The verifier takes its scheme
  from the tag and requires the signed version to agree. Selecting a scheme from a field you have not yet
  authenticated is the downgrade path this design exists to close.
- **Keep the signing key in a secret manager, and pass the issuing host's key when verifying.** Verifying
  with no key is refused rather than reported as a failure, so a missing setting cannot masquerade as an
  altered document.
- **Apply the shipped migration before deploying a build that persists the certificate payload.** On
  Postgres the certificates table gains a `payload` column; the store verifies its own schema at startup
  and fails closed if it is absent, so an un-migrated database is reported at deployment rather than on a
  data subject's request.
- **Run the host's startup pipeline in production.** Startup is where a provisioning fault is reported as
  a provisioning fault. A consumer that skips it still fails closed on first use, but learns about a
  mis-provisioned database from a data subject's request rather than from a failed deployment.

- **Something must call the erasure completion processor.** `AddErasureScheduler()` does. Without it, call
  `IErasureCompletionProcessor.CompletePendingErasuresAsync` from a timer, a scheduled job or an admin
  endpoint. Register `AddErasureVerificationService()` so it can ask the provider. A request awaiting key
  destruction that is never revisited stays in that state. It is honest, not completed.
- **Choose Azure Key Vault purge protection knowingly.** Immediate erasure there requires purge protection to
  be off and a credential with the `purge` permission. Purge protection is the vault's defence against
  accidental or malicious key deletion, and turning it off to erase sooner is a trade-off the consumer makes.
  The framework does not make it for them.

## Evidence

- **The ledger row names its handle** -- `ALedgerRowNamesItsHandleShould`:
  `NotReportADestructionAtOneHandle_AsADestructionAtAnother` records a destruction at one handle and requires
  a different handle's identically named generation to report NOT destroyed, with the liveness half in the
  same arm so a ledger answering false for everything cannot satisfy it;
  `NotTombstoneAnotherHandlesCiphertext_WhenAProviderDerivesOneGenerationForBoth` asks the same question
  through the read path's own inputs, which is where a consumer meets it as a tombstone;
  `RecordBothGenerations_WhenOneHandleIsDestroyedTwice` keeps the property the generation-alone key bought,
  which keying on the handle ALONE would have lost; and `RequireAHandle_OnTheConsumerAssertedWrite` binds the
  public write's refusal of an absent handle. Per provider and against real infrastructure, the same property
  is `ErasureStoreConformanceTestKit.IsGenerationDestroyedAsync_ShouldNotReportOneHandlesDestructionForAnother`,
  run by the in-memory, PostgreSQL and SQL Server conformance suites. Measured, not predicted: keying the
  in-memory ledger on the generation alone turns three of the four unit arms and the conformance arm RED,
  while every other arm in both suites stays green -- so nothing else in the suite was covering this.
- **Cross-tenant crypto-shred isolation** -- `ATenantQualifiedSubjectKeyHandleShould`:
  `TenantBsFieldsStillDecrypt_AfterTenantAErasesTheSameSubjectId` runs the interleaving named in the
  guarantee above over the real stack -- the real keyed hasher, the real AES-GCM provider over a real
  in-memory key store, and the real erasure service writing the real destruction ledger -- and requires
  tenant B's field to still decrypt after tenant A's erasure of the same subject identifier has completed.
  `TombstoneTenantAsOwnFields_WhenTenantAErasesItsSubject` is the liveness half, without which an erasure
  that destroyed nothing at all would satisfy the first arm perfectly.
  `MintADifferentKey_ForEachTenantSharingOneSubjectId` asserts the premise separately, so that a handle
  collision and the data loss it causes are distinguishable failures rather than one aborted arm.
  `DeriveDifferentHandles_ForTenantAndSubjectSplitsThatConcatenateIdentically` binds the length-prefixed
  composition against the collision plain concatenation would introduce, and
  `DeriveOneHandle_ForEverySpellingOfAnAbsentTenant` binds the write path's default identity to the
  sentinel an untenanted erasure request is persisted as. Measured, not predicted: a derivation that
  ignores its tenant argument turns the first and third arms RED -- the first naming tenant B's field as
  `null` -- while the liveness arm stays GREEN, because under one shared key the erasing tenant's own
  fields are still tombstoned. That is why both halves exist, and it is why a liveness-only arm could not
  have caught this.
- **Certificate integrity, claim by claim** — `EveryClaimOnTheCertificateIsSignedShould`:
  `Change_the_canonical_bytes_when_any_single_leaf_changes` varies each of the 26 leaves the payload and
  its nested records declare and requires the signed bytes to differ;
  `Cover_every_leaf_the_payload_declares` reflects the leaf set off the types and requires a mutator for
  each, so a claim added later reddens until somebody covers it;
  `Produce_identical_canonical_bytes_for_an_unchanged_payload` is the liveness half, without which a
  non-deterministic canonical form would satisfy the first arm while proving nothing.
  `Distinguish_an_absent_exemption_list_from_an_empty_one` keeps "no exemptions were claimed" and
  "exemptions were considered and none applied" as different documents, and
  `Distinguish_two_payloads_whose_claims_differ_only_in_where_a_boundary_falls` binds the collision the
  per-leaf arm structurally cannot see. Measured, not predicted: dropping one leaf of the summary from the
  canonical form turns the per-leaf arm RED naming exactly that leaf while
  `ErasureCertificateSignatureShould.Distinguish_two_certificates_that_disagree_about_what_was_erased`
  stays GREEN — which is why both arms exist.
- **Certificate verification** — `ErasureCertificateVerifierShould`: an unaltered certificate verifies; an
  altered claim, a removed exemption record and a wrong key each report `SignatureMismatch`; an earlier
  scheme's bare tag, an unimplemented scheme, a payload version disagreeing with its tag and a malformed
  tag each report `NotVerifiable`; verifying with no key is refused outright. Measured: a verifier
  returning `Verified` unconditionally turns 10 of these 14 arms RED.
- **Certificate storage round-trip** — `ErasureStoreConformanceTestKit`:
  `SaveCertificateAsync_ShouldRoundTripACertificateThatStillVerifies` signs a certificate, stores it,
  reads it back and requires it to still verify, against the in-memory store and both SQL engines;
  `SaveCertificateAsync_ShouldNotVerifyACertificateWhoseClaimsWereAltered` is its non-vacuity half. This
  is the arm the other certificate arms cannot replace — they compare identifiers, so a store dropping
  every claim satisfies all of them. Measured against a real Postgres engine: before the payload column
  landed this arm reported `SignatureMismatch` on every stored certificate.
- **Claims have somewhere to live** — `EveryPayloadClaimHasSomewhereToLiveShould`: a unit-level tripwire
  that reddens when a claim is added to the payload with no column in the SQL Server store, which restores
  from columns. It runs without a database; the conformance arm above is the real proof.
- **Retention scope** — `RetentionDeclaredScopeShould`: `NeverDeleteAnAnnotatedLoadedTypeOutsideTheDeclaredScope`
  (safety: an annotated, loaded, expired but undeclared type is absent from the policies and its record
  survives, with an in-pass control that the declared record of the same age is deleted) and
  `DeleteADeclaredRecordPastItsRetentionBound_AndKeepOneWithinIt` (liveness). Reintroducing a scan of the
  loaded assemblies turns the safety arm red; handing contributors an empty policy set turns both red.
  `FailAtStartupWhenEnabledWithNothingDeclared` pins the startup refusal.
- **SQL Server and PostgreSQL, both contracts** — `SqlServerErasureStoreTenantIsolationShould`,
  `SqlServerLegalHoldStoreTenantIsolationShould`, `PostgresErasureStoreTenantIsolationShould`,
  `PostgresLegalHoldStoreTenantIsolationShould`: tenant-isolation suites run against real containers, with
  safety arms (a tenant that owns nothing must read nothing) and liveness arms (the owning tenant must
  read its own row — a store that returns nothing to everybody passes safety trivially).
- **Capability transparency through the encrypting decorator** —
  `EncryptingDecoratorCapabilityTransparencyShould`: `ForwardTheStoreShapeCapability_ToACapableOutboxInner`
  pins that a delete-on-sent inner still reports that it does not track sent messages once wrapped, and its
  twin `NotAdvertiseTheStoreShapeCapability_OverAnInnerThatLacksIt` pins that an inner which reports nothing
  resolves to nothing rather than to a decorator-supplied default. The payload-bearing capabilities are
  covered separately by `EncryptingOutboxStoreCapabilitySafetyShould`, whose
  `ResolveEveryPayloadBearingCapability_AsAMediatingView_NeverTheRawInner` is the arm preventing a
  capability from becoming a way around the encryption, with
  `ResolveWrappedCapabilities_ToAWorkingMediatingView_OverACapableInner` as its liveness arm.
- **Non-vacuity:** the safety arms were verified RED against a one-token revert of the deployment-mode
  flag, and the read/mutation asymmetry against a revert of the mutation predicate to the read form. Both
  cycles rebuilt the implementation and test projects explicitly; a run against a stale binary proves
  nothing.
- **Encrypt idempotence across a failed write** — `EncryptIsIdempotentAcrossAFailedAppendShould`: the
  safety arm fails the first write, retries the *same* instance as a caller would, and asserts the value
  decrypts in a single pass to the subject's original data; it asserts the retry actually happened, so a
  double that never throws cannot make the arm vacuous. Two liveness arms carry the weight here — an
  ordinary first write must still encrypt, and an **unmarked** legacy value must still be encrypted rather
  than skipped — because a guard that skipped everything would satisfy the safety arm while persisting
  personal data in the clear.
- **Duplicate discrimination, both SQL providers, real containers** — `PostgresErasureDuplicateTranslationShould`,
  `SqlServerErasureDuplicateTranslationShould`, `InMemoryLegalHoldStoreDuplicateSignalShould`: a genuine duplicate must raise the
  specific type and preserve the provider's own exception as its inner exception (liveness), while a
  provider failure that is not a uniqueness violation must not be translated at all (safety). The paired
  arms fail both a blanket catch and a filter narrowed until it never fires.
- **Provisioning faults, both SQL providers, real containers** — `PostgresErasureProvisioningFaultShould`,
  `SqlServerErasureProvisioningFaultShould`: an unprovisioned store must raise the
  provisioning type, and that type must not be assignable to `InvalidOperationException` (safety), while a
  provisioned store must still start and still store (liveness). The startup arms resolve the hosted
  service through the real registration path rather than constructing it, so a registration that
  contributes no validator fails rather than passing quietly.

- **SOC 2 attestation: NO EVIDENCE EXISTS, and the distinction matters.** No conformance arm, unit test
  or governance check RED-detects a report that asserts a verdict on a criterion nothing assessed. This
  is **not** the ordinary "unverified" case — a guarantee we believe and have not yet proven. It has been
  **measured false**: the behaviour described under the attestation guarantee above was reproduced by
  reading the enumeration and aggregation paths, and the affected set is the default configuration rather
  than an exotic one. Treat every statement a generated report makes about an unassessed criterion as
  unsupported until an arm exists that fails when the report asserts one. **Do not cite this subsystem's
  reports as evidence of coverage.**

- **Erasure completeness is established, not merely uncontradicted.** An erasure is reported `Completed`
  only when the `[PersonalData]` annotation scan that underwrites its coverage **actually ran to
  completion**. The scan reports whether it was established alongside what it found, and an unestablished
  scan blocks completion rather than contributing an empty set of gaps.

  The distinction is the guarantee. The uncovered-annotated set is derived from the categories the scan
  returned, so a category the scan never observed is absent from that set for exactly the same reason a
  properly covered one is — making a narrowed scan and a clean one the same observation. That is the state
  a trimmed or ahead-of-time host produces, where whole-domain reflection cannot see what the trimmer
  removed, and reading it as coverage is a certificate attesting to an absence of evidence.

  Enforced at the value set rather than by inspection: the scan result carries its own establishment, so a
  caller cannot obtain the categories without it. Locked by
  `ErasureCoverageGateShould.RefuseToCompleteWhenTheAnnotationScanWasNotEstablished`, with
  `StillCompleteOnAnUnestablishedScanWhenErasureIsCryptoShredOnly` as its liveness partner and
  `ReflectionAnnotationScanReportsEstablishmentShould` binding the producer, so the flag cannot be
  satisfied by a scan that never sets it.

  **Consumer obligation.** Crypto-shred-only erasure is exempt, because coverage there is established by
  destroying the key rather than by locating annotated data. Every other configuration must run erasure on
  a host where reflection over the domain model is available; this is an administrative compliance path,
  not a hot path.

## Known gaps

These are stated because a guarantee with no enforcing test is documented, never asserted.

- **A legal hold placed while an erasure is destroying keys cannot un-destroy the keys already gone.**
  The hold is re-checked before EVERY key destruction, not once per run, so a hold that arrives mid-pass
  stops the destruction at the next key and no later key is touched. The window is therefore one key wide
  rather than the whole discovery pass — but it is not zero, and it cannot be: the hold is recorded in this
  framework's store while the destruction happens at an external key manager that cannot roll back, and a
  legal hold is externally mandated so it cannot be refused to make room for a lease. When this happens the
  erasure STOPS, the request cannot reach `Completed`, and the number of keys already destroyed is recorded
  with the request, so the conflict is visible to an auditor rather than silent. **Consumer obligation:** if
  a hold may be placed against a subject whose erasure is already running, treat the recorded
  already-destroyed count as authoritative — that data is gone and no replay restores it.
- **Retention enforcement is not hold-aware.** No legal hold blocks a retention deletion unless the
  contributor performing it checks one. The built-in outbox and inbox contributors delete messaging records
  by age and do not check holds.
- **Key confirmation is proven against provider test doubles, not live key stores.** The arms drive the real
  providers with faked SDK clients: the Key Vault `KeyClient`, the AWS KMS client and the Vault Transit engine.
  Whether a live vault answers a purged key with 404 on its deleted-keys collection, whether AWS removes a
  deleted key's alias, or whether Transit's delete really leaves no recoverable copy, is taken from the
  providers' published behaviour and is not verified here.
- **A key provider that does not implement `IKeyDestructionStatusProvider` leaves erasures waiting
  indefinitely.** This is the safe direction: such a provider is never confirmed early, but an erasure that
  destroys its keys never completes. The startup Warning is the only signal.
- **Records affected are not carried across the wait.** A request completed at confirmation records the
  keys it confirmed and zero records affected, and its certificate says so in a warning. The counts from
  execution are in the request's message.
- **The confirmed key set is re-derived at confirmation, not recorded at execution.** It is the subject key
  plus every key the inventory associates with the subject at that time, by logical name. A host that confirms
  without the data-inventory wiring the executing host had, or an inventory that forgets a location, shrinks
  that set to the subject key, and a location key still pending destruction would then go unchecked. Closing
  this requires recording the scheduled keys' provider identities durably when the request enters
  `AwaitingKeyDestruction`, which is an erasure-store change not made here.
- **A name that stops resolving is read as destroyed.** Confirmation asks by alias or key name. If an alias is
  deleted by hand while its CMK is still pending deletion, or a Key Vault key is in neither collection
  mid-recovery across all three reads, confirmation can succeed early. Recording provider identities (above)
  closes it.
- **A key that becomes live again leaves the request waiting indefinitely.** If a scheduled key is cancelled
  or recovered, or the subject key is re-created under its deterministic id by a later encryption, the provider
  never reports it destroyed. The request stays `AwaitingKeyDestruction`, visible but with no deadline and no
  transition to a failure state.
- **Confirmation needs an ambient tenant under multi-tenancy.** Listing waiting requests is tenant-confined,
  so a background pass with no tenant cannot find them; the host must drive the processor per tenant. The
  scheduled-execution pass has the same property today.
- **The scheduler's reset is still a read followed by a write.** The guard above removes the observed
  interleavings, but a store-level conditional transition is what would make it atomic.
- **The annotation scan cannot detect a trimmer that removed annotations without raising.** Establishment
  is cleared when reflection is unsupported, when an assembly's types cannot be loaded, and when an
  attribute read throws. A host that trims annotations away *silently* — leaving a scan that runs cleanly
  over fewer members — is not distinguishable from inside the scan. The published contract for such a host
  is therefore the exemption above, not a detection.
- **The producer's negative paths are unproven.** The arm binding the scan establishes the positive
  direction in a just-in-time host. The ahead-of-time case is a different publish mode, and the two
  skip paths need an assembly whose types deliberately fail to load; neither is reachable from the
  existing suite.

- **PostgreSQL is fixed but unverified.** The structural change is in place, but no test runs against a
  real PostgreSQL server, so nothing would RED-detect a regression there.
- **Two safety arms are not proven non-vacuous.** The arms covering a caller who names another tenant
  pass under the mutant for an incidental reason — the seed row carried no tenant before the fix, so the
  named-tenant filter matched nothing either way. They bind the contract going forward; they are not
  evidence that it was broken before.
- **The in-memory store has no schema, so the provisioning guarantee is vacuous for it.** It cannot be
  mis-provisioned and contributes no validator; the guarantee binds the SQL providers only.
- **The data-inventory store is a different contract and is not covered by this document.**
- **The audit store and the general compliance store hold tenant-owned rows and are neither verified nor
  covered by the startup check.** They are excluded deliberately rather than gated blind: failing a host
  closed on a store whose isolation has not been demonstrated would trade a real outage for an unproven
  guarantee.
  The general compliance store — consent records, erasure logs and subject-access requests — ships in exactly
  two implementations, enumerated here because the guarantee above does not cover them: a Postgres store in
  `Excalibur.Compliance.Postgres` and a MongoDB store in `Excalibur.Compliance.MongoDb`. There is no
  in-memory implementation of this contract, and after the provider split **this package ships none of them**,
  which is what keeps the MongoDB and Postgres drivers out of a consumer that uses neither. The split moved
  assemblies and changed no behaviour: the tenant term each store binds is unchanged by it. Both stores
  partition by tenant — the tenant participates in the MongoDB document key and in the Postgres uniqueness
  constraint, so it is the upsert conflict target rather than a filter applied afterwards — but neither is
  exercised against a real server here, so that is described, not asserted.

### Erasure certificate and data-location registry — known gaps

- **No annotation registers a data location.** `[PersonalData]` classifies a property; it carries no
  table and no field, so nothing derives a data location from it. Registration is an explicit call to
  `IDataInventoryService.RegisterDataLocationAsync`, which means a certificate covers exactly the
  locations a host registered. Personal data in a store the host annotated but never registered is not
  erased, and nothing on the certificate distinguishes that from a store that held nothing.
- **A contributor's report is recorded, not verified.** A registered location is discharged when a
  contributor says it took responsibility for that table-and-field pair and resolved it — including
  resolving it as "there was nothing here". The framework does not re-read the store to check. This is
  why `Verification.Verified` is `false` on a certificate whose erasure was discharged by record
  deletion: the certificate says plainly that the framework verified nothing itself.
- **A completion refused because the request moved is reported as a not-found request.** The store
  refuses to stamp `Completed` over a status that took the request out of the run, and says so in the
  message — but it raises the same exception type it raises for a request that does not exist. A caller
  that branches on the type alone cannot tell "a legal hold stopped this" from "no such request".
