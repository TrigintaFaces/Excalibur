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

**The comparison is ordinal, and the argument is matched against the recorded OWNER.** It folds no case,
trims no padding, and matches no prefix, because the owner term is composed into the per-subject key
handle exactly as stored — so two identifiers differing only in case are two tenants, and a comparison
that folded them would offer one tenant a request whose execution destroys the other's key. It is matched
against `ErasureStatus.TenantId`, never `ErasureStatus.TenantPartition`; the two answer different
questions and an implementation filtering on the partition appears correct wherever they coincide. The
full contract is stated on `IErasureQueryStore.ListRequestsAsync`.

**Evidence.** `Conformance/ErasureStoreConformanceTestKit.cs` holds three arms, and they are separable
rather than redundant — each detects a defect the others pass:
`ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant` reads back the owner the store itself recorded
and fails against a filter that is ignored or inverted; `ListRequestsAsync_AcrossOwners_ShouldNotReturnAnotherOwnersRequest` files two requests under two
resolved owners, the first a strict prefix of the second, and is the only arm that fails against a prefix or "starts with" match; and
`ListRequestsAsync_TenantFilterIsOrdinal_ShouldNotFoldCaseOrPadding` is the only arm that fails against a
case-folding comparison — including one folded by a database collation rather than by code. Measured
against the in-memory store: dropping the argument reddens all three, folding case reddens only the
ordinal arm, and prefix-matching reddens only the cross-owner arm.

**Known gaps in that evidence.** The two cross-owner arms need the suite to resolve a named tenant, which
it declares by overriding `BeginOwnerScope`; a suite that does not reports an unverified arm rather than
passing silently, so cross-owner isolation is **UNVERIFIED** for any store whose suite leaves it
unoverridden. Nothing yet certifies that the page window is taken over the filtered set rather than over
an unfiltered page that is then filtered — every list arm runs well inside one page, so a store that
paged first and filtered second would lose rows silently above its page size and pass. And no arm
exercises a tenant filter together with an ambient tenant term, because no shipped suite activates
multi-tenancy; the interaction those stores' own comments are most confident about is the one with no
test behind it.

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
**escaped segment** composition of the tenant identifier and the subject's pseudonymisation token,
using `SegmentedKey.Compose`. Escaping preserves segment boundaries: plain concatenation makes tenant
`a` with subject `bc` one input with tenant `ab` and subject `c`, which would introduce a cross-tenant
collision in the act of removing one. The result is 64 uppercase hexadecimal characters -- the same width and character set as
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

**The exact resolved owner is preserved**, through `SubjectKeyHandle.OwningTenant`.
The explicit default tenant and the untenanted storage partition remain distinct owners. Missing or
blank owners are rejected, not normalized or silently replaced. The write path supplies its captured
owner; erasure supplies the owner persisted on its request. Both paths must therefore name the same
owner deliberately, rather than relying on ambient context when background processing runs.

**The owner a request records is resolved from the HOST, and a request naming a different one is
refused.** This paragraph previously stated the sentence above as a requirement and nothing enforced it:
each erasure store resolved the persisted owner from the value on the incoming request, so a request
naming any other tenant was accepted and became the owner the destruction derived its handle from. The
write path had encrypted under the host's resolved term, so the two disagreed and the erasure destroyed a
handle nothing had minted -- no key found, no error recorded, request completed, certificate signed, data
still readable. A requirement with no enforcement site is the shape this document exists to prevent, and
it had one here.

Resolution is now a single function, `Erasure/ErasureRequestOwner.cs`, called by all three erasure stores
rather than computed inline in each -- the in-memory, PostgreSQL and SQL Server stores previously carried
the same expression character for character, which is one contract implemented three times and an error in
it replicated three times. Under multi-tenancy the resolved ambient scope is authoritative; otherwise the
ambient context's own resolved identity is, because that is the term `CryptoShredding/FieldEncryptor.cs`
encrypts under. **The ambient SCOPE and the ambient CONTEXT are not interchangeable here**: in
single-tenant mode the scope is the reserved untenanted partition while the context resolves the
deployment's default identity, so resolving from the scope would reintroduce the same divergence with the
signs reversed. A request whose tenant is unset accepts the resolved owner; one naming a different tenant
is rejected by name rather than silently corrected, because substituting the right term without a word
would discard what the caller asked for and tell them nothing.

**Evidence.** `tests/unit/Excalibur.Compliance.Tests/Erasure/AnErasureOwnerComesFromTheHostNotTheRequestShould.cs`
RED-detects a violation: its safety arm fails against the previous resolution, and it is paired with two
liveness arms so a resolver that refused every request -- which would satisfy the safety arm while making
every erasure unfileable -- does not pass. **Known gap:** those arms exercise the shared resolver through
the in-memory store only. The other two providers are covered because they call the same function, not
because an arm ran against a database, so a provider that stopped calling it would not be caught there.

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

**Guarantee: an aggregate type declared through `AddErasureRetention` **in the view of the instance that
executes the erasure** is not tombstoned, its snapshots are not deleted, its read-model rows are not
cleared, its `[PersonalData]` fields written while that declaration was held still DECRYPT after the
subject's key is destroyed, and the erasure record names the type with the lawful basis, the written
justification, and the period it is kept under. This holds for EVERY data subject named inside a retained
type, not only the one the obligation was written with in mind. An aggregate type that is NOT named is
erased exactly as it is with no retention declared at all.**

**THE QUALIFIER IS LOAD-BEARING AND THIS SENTENCE USED TO OMIT IT.** It read *"written while the
declaration was in force"*, unqualified, and **"in force" has no deployment-wide referent**: each process
builds its retention registry in its own constructor from declarations written IN CODE, and nothing
compares one instance's set against another's. There are only per-instance views, so the unqualified
sentence quantified over an instant that does not exist, and it was therefore FALSE during any rolling
deploy that adds a retention. The guarantee above is the true one; the superseded wording is quoted here
because it is the sentence a reader may have acted on.

**THE WINDOW, in falsifiable terms.** During a deploy that ADDS a retention declaration, with more than one
instance live, an instance that does not yet hold the new declaration can execute an erasure and
irreversibly destroy the key, the escrow and the recovery tokens for an aggregate type the deploy declares
retained — and certify it `Completed`, because by its own view the erasure was correct. The loss is
confined to types declared in that deploy and to subjects erased inside that window. **A single-instance
deployment cannot reach it**, because there is no other view to be behind.

**Consumer obligation until the deployment-wide record lands:** quiesce erasure execution across a deploy
that adds a retention declaration, or ensure every instance holds the new declaration before any instance
serves. See *Consumer obligations* below.

**A STALE INSTANCE NOW WITHHOLDS THE DESTRUCTION, and the predicate is CONTAINMENT.** A durable,
deployment-wide record holds the declared `(tenant, aggregate type)` pairs. Each instance records its own
once per process, and before any destruction the executing instance refuses when **the record holds a live
declaration it does not hold**.

**Containment, not "the local set is a strict subset of the record" — and that distinction is a reachable
counter-example rather than a preference.** The subset form conjoins the real hazard with a condition that
is not a safety condition at all (that this instance knows nothing the record does not), and that second
conjunct OPENS the gate precisely when an instance is behind in one pair and ahead in another:

```
v2 declares {A, C}   advances fully                 record = {A, C}
v3 declares {A, B}   advance records A, FAILS on B  record = {A, C}
strict subset: is {A,B} a subset of {A,C}?  No  -> PASSES -> v3 destroys C's key
containment:   does v3 hold C?              No  -> REFUSES
```

A partial advance is reachable by construction, because SQL Server has no single-statement
insert-if-absent and records pair by pair.

**Containment makes a PARTIALLY FILLED record sound to compare against. It does NOT make the advance's
completion irrelevant to safety, and the earlier wording of this paragraph said it did.** Superseded, quoted
so a reader who absorbed it recognises it: *"Containment also makes the advance's COMPLETION irrelevant to
safety, which matters because N inserts cannot be made atomic."* What containment actually buys is that a
prefix never produces a FALSE REFUSAL and never makes the comparison unsound with respect to what is
recorded — which is the part that matters, because N inserts cannot be made atomic. But an **unrecorded**
pair is invisible to every instance, so for that pair no stale instance can detect its own staleness, and
the destruction proceeds. Completion is therefore safety-bearing for the pairs that did not land, which is
precisely why the latch closes only on complete success. A model check refutes the latch-on-prefix design in
three steps: one instance declares a retention, its advance writes none of it, the latch closes anyway, and
a second instance holding no declarations passes containment against an empty record and destroys the
retained type.

**The comparison runs twice, and only the later one is the safety mechanism.** The record only grows, so
the earlier read observes a subset of what the later one observes and can only ever refuse LESS. The
pre-claim comparison is a LIVENESS device: it refuses before the execution claim, so the request stays
scheduled and a current instance takes it on the next poll — where a refusal after the claim would have
nowhere to put the request. The comparison immediately before the first destruction is the whole safety
property, and ONE refusal there covers both readers of the declaration set, the key spared by the executor
and the rows spared by the event-store contributor. Per-key refusal would be unsound: a key destroyed
while the rows are spared leaves retained records present and permanently unreadable.

**A REFUSAL IS A DELAY, NOT A FAULT.** It is reported as a refusal rather than a failure, so it does not
spend the request's retry budget — which, at exhaustion, would have written a terminal status that no pass
enumerates and converted a correct withholding into a permanent non-erasure.

**The advance is recorded from the scheduler's poll, not before this instance's first erasure.** The
advance protects OTHER instances from this one, so anchoring it to this instance's first erasure would
begin that protection days after the deploy in a low-volume deployment, and would leave the first
post-deploy erasure decided by poll timing. It is deliberately not a readiness check: a compliance-store
outage withholds this instance's erasures and does not stop it serving. **A PARTIAL advance never counts
as advanced** — an unrecorded pair is a pair for which no stale instance can detect its own staleness,
with no tell anywhere — so the latch closes only on complete success and retries on the next poll.

Bound by `AStaleRetentionViewWithholdsDestructionShould` — six arms, each mutant run and attributed:
replacing containment with strict subset reddens the incomparable-state arm; treating a tombstone as live
reddens the withdrawn arm; closing the latch before the record confirms reddens the retry arm. Its
liveness arms assert that an instance holding MORE than the record proceeds, and that a deployment with no
record proceeds — without them every arm here is satisfied by a gate that refuses everything, which is its
own regulator's finding.

**WHAT IT DOES NOT CLOSE, and this is a property of the problem rather than of the design.** The window
narrows to "until the first instance has recorded its declarations"; it does not close. At the instant
before any instance has recorded anything, *"the pair was declared and nothing recorded it"* and *"the pair
was never declared"* differ in no variable any running process can observe, so no mechanism confined to the
fleet can separate them. The consumer obligations below are what close the remainder, and they survive the
mechanism.

**ALSO NOT DONE: an instance does not act on a TOMBSTONE.** A withdrawn declaration correctly stops making
an instance look stale, but an instance whose own code still declares a withdrawn retention continues to
spare that type. Refusing the erasure there would be the wrong direction — the harm prevented is
over-retention of one type's key, while the harm caused is the subject not being erased at all — so acting
on it means dropping that one obligation, which is a change in two packages and is tracked separately.

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
destroyable key with no envelope machinery (`CryptoShredding/SubjectKeyManager.GetOrCreateKeyAsync`).

**EVERY aggregate type's handle is widened, declared or not, and the write path reads no retention
declaration at all.** The superseded design widened only a DECLARED type's handle, and this document
asserted the consequence as a guarantee -- *"every other value stays under the subject's own handle, which
is the one the erasure destroys unconditionally and the only one it can enumerate."* That sentence is
quoted here because it was WRONG and a reader may have absorbed it. Widening only for a type declared AT
WRITE TIME puts every non-aggregate write and every undeclared-aggregate write on the SAME handle, so a
retention declared afterwards binds two obligations to one key with opposite required values: destroying
it makes legally retained data unreadable, and sparing it leaves personal data recoverable for a subject
reported erased. That is reachable with no concurrency at all.

**So the erasure no longer re-derives the handles it destroys; it ENUMERATES them.** The write path
records every handle in a durable ledger BEFORE minting the key at it
(`IKeyMintLedger.RecordMintIntentAsync`, called from `SubjectKeyManager.GetOrCreateKeyAsync`), and the
erasure destroys the union of the handles it can derive from declared retentions and the handles that
ledger reports (`IKeyMintLedger.ListHandlesAsync`, read in `Erasure/ErasureService.ExecuteAsync`).
Recording before minting is the load-bearing order: the reverse admits a crash leaving live key material
with no row, which nothing would ever enumerate. A row with no key behind it is handled -- the provider
answers not-found and nothing is attested from the absence. The erasure additionally refuses to destroy a
discovered key that resolves to a retained handle. The decrypt path needs no change, because the envelope
carries the handle that produced it.

**A WRITE THAT LANDS WHILE AN ERASURE RUNS IS DETECTED, AND THE ERASURE REFUSES TO CERTIFY.** The
mechanism is a two-state row, and the reasoning behind it is worth stating because the obvious remedies do
not work. The ledger row is written BEFORE the key is created, so while a write sits between those two
steps it is invisible to both observables an erasure has: the ledger's handle set is unchanged since the
erasure read it, and the key material reads absent because it genuinely is not there yet. That state is
**observationally identical** to a writer that crashed between its row and its mint -- which is safe and
correctly ignored -- and the two diverge only after the certificate is signed. So re-reading the handle
set buys nothing, and no number of material probes separates them: the key store cannot tell "not yet
created" from "destroyed".

What separates them is the row itself. `RecordMintIntentAsync` writes it as PENDING;
`MarkMintedAsync` confirms it once the key exists (`SubjectKeyManager.GetOrCreateKeyAsync`). An erasure
destroys the union of both sets, re-reads the ledger at attestation, and **raises an error for every
handle still pending**, which makes `Completed` unreachable and yields
`CompletedExceptConcurrentWrites` naming those handles. The superseded text of this section is quoted
below, because it described the gap as open and a reader may have acted on it.

> *"A write for an obligation class with no declared retention, recording its handle after that read and
> minting after the destruction loop, leaves a live key holding this subject's personal data under a
> certificate that reports `Completed` and does not name it."*

**Confirming a mint FAILS OPEN, deliberately, and the cost is stated rather than hidden.** A lost
confirmation leaves the row pending, which withholds a clean certificate rather than granting one -- the
conservative direction. A consumer's personal-data write is never failed because a confirmation could not
be recorded. The consequence is that erasures for that subject report a concurrent write until some later
mint at the same handle confirms the row, and the framework logs that at `Error` when it happens.

**KNOWN GAP, AND IT IS PERMANENT RATHER THAN TRANSIENT -- an earlier wording of the sentence above
implied otherwise and that implication was the defect.** A row is confirmed only by a later mint at the
same handle. A writer that dies between writing its row and creating its key leaves the row pending with
no later mint coming: rows are insert-only and permanent by contract, there is no abandon record, and no
operator remedy exists. So every subsequent erasure of that data subject reports
`CompletedExceptConcurrentWrites` and advises erasing again once the writes have stopped -- and the
writes have stopped. **That subject can never reach `Completed`.**

This is the designed indistinguishability and no amount of probing closes it: a row whose mint has not
run is identical, in both observables an erasure has, to a row whose writer crashed. Closing it needs a
terminal disposition for a stale pending row -- an age bound the consumer sets, or an explicit abandon
record written by the write path -- and neither exists. **Consumers running an erasure against a
deadline must treat a permanently pending row as an operational condition to detect, not as a transient
one to wait out.**

**THE ATTESTATION QUANTIFIES OVER WHAT THE LEDGER HOLDS, NOT OVER WHAT THIS PASS DESTROYED.** Before the
attestation signs anything it re-reads the subject's mint ledger and probes every confirmed handle on it,
less two sets: the handles this erasure deliberately retained, and the handles that already raised an
error of their own during the destruction. A handle holding live material that is in neither raises an
error, which makes `Completed` unreachable, and the outcome says a write arrived alongside.

**The second subtraction is the handles that REPORTED, never the handles the pass LOOKED AT**, and the
difference is load-bearing rather than pedantic. Subtracting everything the erasure enumerated was tried
and is wrong: the destruction's absent branch is an idempotent no-op by design — a handle whose key never
existed has no ciphertext this erasure could have made unreadable — so it raises no error and records no
coverage. A handle whose ledger row existed before the enumeration and whose key was created after the
destruction attempt is therefore enumerated, acted on, and reported by nothing. **Enumerated is an
intent; destroyed is a fact**, and the set where they differ is the blind spot of any check that
subtracts the enumerated set.

The predicate is *is the key material gone*, never *is this handle in a list the pass kept*, and that
choice is what keeps a retry honest: a handle an earlier pass destroyed probes as absent and is excused by
the probe itself, where a list-membership formulation would have to carry every earlier pass's handles and
would indict its own first pass the moment it forgot one.

> **This superseded a narrower check, and the superseded wording is quoted because a reader may have
> absorbed it:** *"the window is one round trip wide instead of spanning the whole
> enumeration-to-attestation period, and the dominant case -- a write already in flight when the erasure
> begins -- is caught."* Both halves were true of a write already IN FLIGHT and false of one that started
> **and finished** inside that period. For such a handle the window was the whole period, and the handle
> was in none of the three sets anything examined: not in the enumerated set, which predates it; not in
> the destroyed set, so the re-occupation probe never reached it; and not pending, because its mint
> completed, so the two-state row never reached it either. The certificate was signed over live key
> material with nothing raised. It was found by removing two grants from the formal model, not by a test.

**KNOWN GAP -- it DETECTS a concurrent write, it does not EXCLUDE one.** Two parts remain open. First,
the attestation re-read and the completion write are not one atomic action at the store, so a row
inserted between them is not seen; closing it needs the completion write to be conditional at the store
on there being no pending row for the subject. **That window is the probe loop, not a single round
trip** — the ledger read moved ahead of the destruction-status probes because it supplies their domain,
which means a row landing during the probes is no longer observed, and there is now one provider call per
candidate rather than one in total. It is a trade for the whole-ledger probe above, which closes a wider
and entirely silent hole, and it is recorded as a trade rather than as a free widening. Second, nothing orders a concurrent write against
an in-flight erasure, so a subject under continuous write load can keep receiving
`CompletedExceptConcurrentWrites` and never reach `Completed`.

**One case is reported less precisely than it could be, stated rather than left to be discovered.** A
handle this request's own record says it destroyed, found live again, is excluded from the
concurrent-writes outcome and falls to a partial outcome instead -- including when this pass genuinely
destroyed it and a second write re-occupied it. The concurrent-writes outcome asserts that everything the
erasure set out to destroy is gone, and the request's own record contradicts that, so the imprecision runs
in the direction of not claiming success.

**Consumer obligations.** Quiesce writes for a data subject before executing their erasure: a write
racing the erasure makes that erasure report a concurrent write rather than complete, and an erasure that
must terminate by a deadline cannot be left to chance. Do not rotate the key your `IDataSubjectHasher`
derives from -- every pseudonymisation token changes at once, and every mint row recorded before the
rotation becomes unfindable while remaining perfectly durable, so an erasure after a rotation enumerates
nothing for every subject at once.

This subsystem's blast radius is catastrophic and its rigor floor is R4; **R3 and R4 are UNVERIFIED** for
the enumeration. The formal model under `spec/tla/` no longer grants the two checks it used to -- the
coverage set is the set the request enumerated, as the implementation's is, and there is no conjunct
barring certification while a write is mid-flight. Removing them turned both shipped-design arms red on
a reachable violation, which is how the gap quoted above was found. The arms now separate the mechanisms
one at a time: one refutes the design without the two-state mint row, one refutes it with the attestation
scoped to the enumerated set alone, one refutes it with the attestation re-read and the certificate as
separate actions, and one requires a useful certificate to stay reachable so the others cannot be
satisfied by a design that reports a concurrent write for everything and erases nothing.

**The rung is still UNVERIFIED and a green suite is not a discharge.** One grant remains and is a declared
constant rather than an assumption built into the model's shape: the attestation re-read and the
certificate are one action there and two in the code, which is the first remaining gap above. Read the
model's scope statement before citing any arm.

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

**Guarantee: `VerificationResult.Verified` is `true` only when some configured verification method
actually substantiated THIS execution and nothing was recorded against it.** Formally,
`Verified ⟹ Methods ≠ None ∧ Failures = ∅`. `false` means *not established*, for any reason, and is
never a claim that the erasure itself failed. The seam is `ErasureVerificationService.cs:207`, where
both conjuncts are evaluated together, so a result reading `Verified = true, Methods = None` —
verified by nothing — is inexpressible rather than unlikely. The liveness half matters as much: an
erasure the framework DID substantiate still reports `true`, so "refuse everything" is not a
conforming implementation.

This is the same property the certificate producer above holds, stated for the other producer. The
two were previously enforced differently: the certificate derives both fields from one value, while
this verdict filtered its recorded failures by severity and waived anything below `Critical`. That
filter was correct for the set of failures that existed when it was written and did not change when a
lower-severity producer was added, so an erasure with no key deletions on record could report
`Verified = true` while carrying a failure whose own text said key destruction could not be verified.
Both producers now express the property directly.

**Evidence.** `NonVacuousErasureVerificationShould` carries three arms that RED-detect a violation:
one for a recorded failure being waived, one for a method that could not substantiate being reported
as used, and one for the configuration in which no method runs at all. The third is the arm that
distinguishes the predicate from the property — it stays red if only the severity half is corrected.

**Known gaps, by behaviour.**
- `VerificationSeverity.Error` has no producer, so the three-state severity enum does not currently
  discriminate at this seam. The verdict no longer depends on it.
- `Verified` is a `bool` over a three-valued question: it cannot distinguish *we looked and it failed*
  from *nobody looked*. `Methods` and `Failures` together carry that distinction, and a consumer that
  needs it must read them rather than the boolean.
- A verification method set of `None` is accepted by configuration validation. The verdict refuses
  rather than reporting success, so the outcome is honest, but the configuration is not rejected at
  startup.
- These arms are sampling-class and run against fakes; they bind the verdict algebra and say nothing
  about key-destruction semantics. **This seam's blast radius is catastrophic and its rigor floor is
  R4; R3 and R4 remain UNVERIFIED and the arms above discharge neither.**

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
gives DEDUP: a second confirmer's insert is refused as a duplicate and it records completion against the
existing certificate. The scheduler never resets a request to `Scheduled` when it is `InProgress`,
`AwaitingKeyDestruction`, `Completed` or `Cancelled`, because it cannot tell its own failure from another
instance's ownership.

*Superseded wording, quoted so a reader who absorbed it recognises it: "the certificate insert **is the
claim**". It is not, and the distinction became load-bearing when custody acquired columns of its own. The
derived id DEDUPLICATES; the claim EXCLUDES. Both are kept and they do different jobs — the derived id
additionally covers a confirmer that crashed after inserting the certificate and before recording
completion, whose successor is a different claim entirely.*

## Custody: who holds an erasure, and what a superseded holder may still write

**Guarantee: an erasure request records WHO holds it and UNTIL WHEN, and a write that advances the
attestation is applied only for the holder of the claim the request currently carries.** A caller whose
claim has been superseded is told so and writes nothing. Custody lives in two columns of its own
(`claim_identity`, `claim_expires_at`), paired all-null-or-all-non-null by a check constraint, and is
taken through exactly one member — `IErasureStore.ClaimForExecutionAsync`. Writing the claimed status
through the general status member is refused outright, so custody cannot be granted to a caller that has
no identity to be given.

**What this replaced, stated because the shape is the lesson.** The claim was already atomic: one
conditional UPDATE, exactly one caller of N. What it did not carry was an owner or a deadline, so an
execution interrupted by a host shutdown stayed in-progress forever — every query that looks for work
correctly excluded it, nothing threw, nothing logged, and the subject's data was not erased. The status
column was being asked to carry two independent facts, OUTCOME and CUSTODY, and no new status value was
added: custody moved to its own axis instead, which is why a recovered request stays in the claimed
status, carrying no live claim, and is re-offered by the lapse predicate rather than reset.

**A LAPSE DOES NOT REVOKE; A RE-CLAIM REVOKES.** Nothing tells a holder its lease elapsed, and nothing
needs to. A holder whose run outlasted its own lease but whom nobody superseded still holds the identity
the row carries, and its attestation writes still apply — otherwise an executor that had already
destroyed a subject's keys could be refused its own completion, leaving the erasure performed and no
certificate able to attest it. The expiry is therefore consulted in the CLAIM's predicate and in the
recovery path, and never in a fenced write, which compares the identity and only the identity.

**The expiry is written and compared BY THE STORE.** The SQL providers use the database's own clock inside
the claiming statement; the in-memory store takes a `TimeProvider`. A caller-computed instant would make a
lapse a comparison between two machines' clocks, which is the one comparison that may never decide
anything: a fast clock hands its holder a claim that has already expired, and a slow one holds a claim
nobody else can take.

**THE FENCE PROTECTS THE ATTESTATION, NOT THE DESTRUCTION, and a reader must not infer the stronger
thing.** A superseded executor learns it lost the request when its first fenced write is refused — by
which time it may already have asked the key provider to destroy keys. That is accepted, and it is
precisely why two members are deliberately OUTSIDE the fence:

- `RecordKeyDestroyedAsync` and `StageKeyDestructionAsync` record a physical act that has ALREADY
  happened. Refusing a superseded writer there would destroy the only evidence that material is gone: the
  generation cannot be read back, because the destruction took it, so no successor could record it either
  and the subject's erasure would become permanently unreportable. The ledger is keyed on the generation,
  minted once and never reused, so two claims recording one generation produce a single idempotent row.
  That sharing is not a defect — it is how a retried erasure attests what an earlier pass achieved.
- A write of `BlockedByLegalHold` is admitted regardless of custody. A hold is a superior authority, and
  refusing one would drop it silently and let the run complete over data the controller has asserted a
  legal basis to retain. The asymmetry is this subsystem's own: withholding an erasure is recoverable by
  releasing or narrowing the hold, and destroying held records is not.

**INVARIANT: THE CLAIM TOUCHES ONLY THE CUSTODY COLUMNS, AND THIS IS WHAT MAKES RE-EXECUTION SAFE.** A
request may legitimately be executed twice — once by a holder that lapsed, once by its recoverer — and
that is safe only because the destroyed-handle and staged-generation sets are durable per request,
appended to and never replaced. A recoverer therefore inherits the coverage an earlier pass established
and can attest it. **A recovery write that also cleared those sets would convert a safe re-offer into a
permanently uncertifiable request**: the generations are gone from the key provider, because the earlier
pass destroyed them, so no successor can re-establish what they covered and the subject's erasure can
never be certified by anybody.

Nothing in the type system prevents it, so it is stated here as an invariant rather than left as a
comment. The claim's own statement sets `claim_identity`, `claim_expires_at` and `updated_at`, and
advances `status` and `executed_at` **only** under a condition that fires when claiming from
`Scheduled` — which is why a confirmation pass claiming an already-staged request does not un-stage it.
It touches no attestation table. Any future recovery or reset path is bound by the same rule.

**NO EXECUTION DEADLINE, AND THE ABSENCE IS A RULING RATHER THAN AN OMISSION.** One was specified,
built, landed and removed in the same session. The ruling it implemented said *the lapse must cause the
owner to stop*; an adversarial review of the built article showed that it does not survive contact with
the destruction loop, and the measurement agreed.

Three reasons, in the order they bite:

- **It never stopped the owner.** The per-key handler in the destruction loop catches every exception,
  `OperationCanceledException` included, and continues to the next key. A fired deadline therefore
  converted each remaining key into a spurious "failed to delete" error and drove the request terminal,
  which is worse than not firing.
- **It could fire between an irreversible act and its record.** A key is destroyed at the provider and
  then recorded in the ledger; a cancellation landing between those two leaves material destroyed with
  no durable record that it was. Once a pass has destroyed something, cancellation is not a safety
  device — it is what creates the unrecorded state.
- **It bought no safety.** The fence already makes a late write safe: a superseded identity is refused,
  and a lapse does not revoke. So a SLOW holder is harmless, while a STOPPED holder is the one state
  nothing can repair. The deadline's only mechanical effect was to fire earlier, deeper inside the loop,
  which is precisely where the unfenced damage is.

**What the lapse needs to cause is a SUCCESSOR, not a stop**, and that is what the entry guard now
provides: `ErasureService.ExecuteAsync` admits a request in the claimed status and lets the store decide
availability in one indivisible step.

*Superseded wording, quoted so a reader who absorbed it recognises it: "THE LEASE IS ALSO THE EXECUTION
DEADLINE, which is what stops a holder from working past its own custody ... The work is cut off at
`ClaimDuration` minus `ClaimDeadlineMargin`." That option no longer exists, and neither does the
deadline.*

**If a deadline is wanted later**, it belongs at the TOP of the destruction loop, between keys, where no
irreversible act is waiting for its record — and the claim must RETURN its expiry rather than have a
caller re-derive it from a local clock, since the expiry is computed by the store and a derived deadline
is a second source of truth for a fact the store already owns.

**LIVENESS: a request whose executor died is offered again once its claim lapses, and the next pass
EXECUTES it rather than refusing it.** Stated falsifiably, because the vague version is what let this go
wrong twice: within one `ClaimDuration` of the holder's last heartbeat-free moment, the request appears
in `IErasureQueryStore.GetScheduledRequestsAsync`; a caller passing it to
`ErasureService.ExecuteAsync` is GRANTED custody and runs the erasure to a terminal, certified status;
and no sequence of calls hands the same request to two executors at once, because the claim is one
conditional update that tests custody before status and exactly one of N callers wins.

**How it is achieved.** Three parts, and each failed on its own at some point: the claim records an owner
and a server-side expiry (`claim_identity`, `claim_expires_at`); the scheduled-request query returns a
request in the claimed status whose expiry has passed, as well as one past its grace period; and
`ErasureService.ExecuteAsync` admits the claimed status at its entry guard and claims with that status,
so the store — not the caller — decides availability. **Remove any one and the request is stranded
while the other two look correct**, which is exactly how the first two landed without the third and the
guarantee read as satisfied.

**Liveness is conditional on something calling the executor, and on nothing else.** The optional
scheduler does it each cycle. **A host with no background service calls
`ErasureService.ExecuteAsync` itself** — from a timer, a scheduled job or an admin endpoint — for each
request `GetScheduledRequestsAsync` returns; that enumeration is the whole contract, and a caller needs
no knowledge of custody to honour it, because a request another instance is actively erasing is refused
by the store and simply reported as not executed. There is no separate recovery entry point and
deliberately so: a second door would need its own exclusivity argument.

**Evidence.** `ALapsedErasureIsActuallyReexecutedShould` binds both halves against the in-memory store:
a request stranded in the claimed status with a lapsed lease is executed to `Completed`, and one whose
lease is still live is refused and left exactly as its holder left it. RED-proven by restoring the
pre-fix entry guard, which fails the liveness arm with the defect verbatim — `Invalid status:
InProgress` — while the safety arm stays green. The provider tier is bound by the conformance kit's
claim and fence arms, green on real PostgreSQL and real SQL Server.

**KNOWN GAP on this property.** The window is bounded by `ClaimDuration` and nothing shortens it: there
is no heartbeat, so a host that dies one second into a fifteen-minute lease leaves the request invisible
for the rest of it. That is a deliberate trade — a renewing lease cannot distinguish a wedged-but-live
owner from a healthy one, and the lapse must fire on the wedged one — but it means the worst-case delay
before a subject's erasure resumes IS this option's value, and a consumer choosing it is choosing that
delay.

**Consumer obligation.** Configure `ErasureOptions.Execution.ClaimDuration` comfortably longer than a real
erasure takes. **It is NOT a timeout on the work** — a holder whose run outlasts it still completes and
still certifies, because a lapse does not revoke custody and only another instance actually taking the
request does. What a short value costs is a second instance recovering a request whose first holder is
merely slow: safe, because the destruction is idempotent and the attestation is fenced, but wasted work
and a superseded executor that stands down part-way.

*This paragraph has now been corrected TWICE IN OPPOSITE DIRECTIONS, which is worth more than either
version. It originally said the lease is not a timeout on the work; a pass narrowed that when the
execution deadline landed; this pass restored it when the deadline was removed. Both corrections were
right about the tree they were written against. The lesson is the one the second correction already
recorded about the first — a claim in a sibling paragraph survives the pass that fixes its twin — and
the remedy is to re-read the artifact for the CLAIM rather than for the text just written.*

**Evidence, in two tiers.** At the PROVIDER tier, `ErasureStoreConformanceTestKit` binds custody against
every store a consumer can plug in, and its three derivers run those arms against the in-memory store,
real PostgreSQL and real SQL Server. RED-proven: answering a second contender with a status mismatch
instead of ALREADY CLAIMED — the one-token defect that strands a request which is still coming back —
reddens exactly that arm and nothing else.

At the IN-MEMORY tier, `TheClaimFenceRefusesASupersededWriterShould` binds the contract with
both halves of every property: a completion presenting a superseded claim is refused AND the current
holder's completion applies; a lapsed-but-unsuperseded holder still completes; a second contender is told
ALREADY CLAIMED rather than wrong-status, because the two oblige different things of the caller; a
recoverer takes a lapsed request AND is refused while the lease is live; and taking custody through the
general status member throws. The fixture state that discriminates is `claimDuration: TimeSpan.Zero`,
which the contract admits for exactly that reason — a fresh request per arm only ever produces "nothing
is claimed", and in that state the fenced and unfenced code agree. RED-proven by two mutants: dropping the
identity term from the completion reddens the superseded arm, and deriving the identity from the request id
instead of minting a fresh one reddens two arms, which is the mutant that leaves the fence structurally
present and semantically inert.

**RETRACTED. THE STRANDING IS NOT CLOSED, AND THIS SECTION CLAIMED IT WAS.** The discovery queries do
now carry the lapse predicate, so a recovered request is OFFERED — and `ErasureService.ExecuteAsync`
refuses it. Its entry guard admits only `Scheduled` (`ErasureService.cs:554`), and the only two
production calls to `ClaimForExecutionAsync` expect `Scheduled` (`:587`) or `AwaitingKeyDestruction`
(`:2125`). **Nothing claims from the in-progress status, so a lapsed request is offered on every poll
and executed on none.** Enumerated, not assumed: those are the only two call sites in `src/`.

So the lease makes recovery possible, the predicate makes it VISIBLE, and no consumer acts on it. That
is the same defect one level up from the one this section was written about, and the correction is owed
a successor rather than another query. **Until a pass can claim from the in-progress status, treat an
interrupted erasure as STRANDED.**

*Superseded wording, quoted because it is the dangerous kind of wrong — a false CLOSED stops anybody
looking again: "CLOSED, and recorded rather than deleted because a reader who absorbed the gap needs to
see it go. The discovery queries now carry the lapse predicate ... Until that landed, a lapsed request
was re-CLAIMABLE but was never re-OFFERED by a background pass, so the lease made recovery possible
without making it happen." Every clause of that is true except the verdict. The author measured the
query and never measured its CONSUMER.*

**What the predicate did achieve, stated so the work is not misread as useless.** The discovery queries
now carry the lapse predicate. `GetScheduledRequestsAsync` returns a request whose
claim has lapsed as well as one past its grace period, and `GetAwaitingKeyDestructionRequestsAsync`
excludes a request a confirmer currently holds while including one whose claim has lapsed. Both are
decided against a clock the STORE reads. Until that landed, a lapsed request was re-CLAIMABLE but was
never re-OFFERED by a background pass, so the lease made recovery possible without making it happen
— the stranding was narrowed and not closed, and this section said so in the present tense.

**KNOWN GAPS, and they are the reason this section does not claim more.**

- **The conformance kit now binds the FENCE as well as custody, and the arms are wired in all three
  derivers.** Three arms were added: a completion presenting a SUPERSEDED claim is refused while the
  current holder's applies; a holder whose lease elapsed but whom nobody superseded still completes; and
  a legal hold under a live claim is admitted and releases custody. Each is RED-proven against the
  in-memory store by its OWN mutant, and the pair is what pins the contract: dropping the identity
  comparison reddens the superseded arm alone, and ALSO comparing the expiry — the obvious reading, and
  the dangerous one — reddens the lapsed-holder arm alone. Neither mutant reddens both, so the two arms
  bind independent properties rather than restating one.

  **AND THEY NOW EXECUTE GREEN AGAINST REAL ENGINES: 57 of 57 on PostgreSQL and 57 of 57 on SQL
  Server, 0 skipped, through the container fixtures.** The first such run found THREE defects that no
  unit suite could see, because the in-memory store cannot express any of them:

  - both verdict statements aliased their counts in snake_case while the types reading them used
    PascalCase, and Dapper's underscore matching is enabled only by a package this one does not
    reference — so every property stayed at its default of ZERO. A blank claim verdict reads as
    "request not found", so **ClaimForExecutionAsync answered NotFound for every request that
    existed, and no erasure could be claimed on PostgreSQL at all.** Silent: no exception, no
    warning, and a plausible-looking outcome;
  - five doubled-brace placeholders in the PostgreSQL status write emitted a literal brace, so the
    statement died with a syntax error. Loud, which is the only merciful thing about it;
  - BOTH providers conflated an ABSENT request with a SUPERSEDED claim, because one query filtered on
    the id and the identity together and zero was read as a lost claim. A completion for a request
    that does not exist was therefore reported as an ordinary race the caller stands down from,
    instead of the exception this member's contract states and the in-memory store throws.

  The arms were written to catch a two-executor attestation race and instead caught total
  non-function, which is what a conformance kit is for: it does not care which defect it finds.

  *Superseded wording, quoted so a reader who absorbed it recognises it: "The conformance kit binds
  CUSTODY against every provider; it does NOT yet bind the FENCE ... Those three properties are bound on
  the in-memory store only." The arms now exist for every provider; only their EXECUTION against the two
  SQL engines is outstanding.*

- **The former gap, kept for the shape of it.** The kit
  takes custody through `ClaimForExecutionAsync` wherever an arm needs a claimed request, and asserts
  that custody is exclusive, that the loser is refused as ALREADY CLAIMED rather than wrong-status, that
  a refused claim carries no identity, and that the claim stamps `ExecutedAt`. All three derivers wrap
  those arms, so they run against real PostgreSQL and real SQL Server. **What the kit does not yet
  assert is the fence itself** — that a completion presenting a superseded identity is refused, that a
  lapsed-but-unsuperseded holder still completes, and that a legal hold is admitted regardless of
  custody. Those three properties are bound on the in-memory store only.

  **Both SQL providers' statements have now been exercised by hand against a real engine**, each on a
  fresh database with all nine migration scripts applied in order. PostgreSQL: a wrong identity on a
  fenced status wrote zero rows, and a legal hold with no identity under a live claim wrote one and
  released custody. SQL Server: the claim from scheduled granted one; a second claim under a live one
  wrote zero; a fenced write presenting the WRONG identity wrote zero; a fenced write presenting NO
  identity under a live claim wrote zero; the row was still held by the first holder afterwards; the
  legal-hold exemption wrote one and cleared both custody columns; and the pairing check constraint
  refused each half alone with error 547.

  **Two controls were part of that run and both matter more than the eight results.** A positive control
  asserts the probe row exists, because every other arm expects ZERO rows and an empty table satisfies
  all of them — which is precisely what happened on the first attempt, when the insert failed on a
  non-null column and four arms reported success against nothing. And the legal-hold exemption is run
  twice, once with a non-hold status, because its predicate is a disjunction whose first term is true
  for a hold: without the second run the arm is a tautology that cannot fail.

  **This NARROWS the gap; it does not close it.** What was verified is the STATEMENTS, not the C# that
  builds them, and a by-hand check is not a standing arm — nothing re-runs it, and it will not catch
  a later edit. The fence therefore remains **UNVERIFIED at the conformance tier** for both SQL
  providers until the kit carries these assertions and the derivers run them.

  *Superseded wording, quoted so a reader who absorbed it recognises it: "The equivalent assertions
  against real PostgreSQL and SQL Server are NOT yet in the store conformance kit." That was true of
  the whole contract when written and is now true of only the fence half.*
- **The in-memory store serialises custody with a per-request lock.** That is stronger than it needs to be
  and is stated so nobody reads it as the contract: what the contract requires is that the status test,
  the status write, the identity and the expiry become visible together, which the SQL providers get from
  a single conditional UPDATE.

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
under racing confirmers; and the scheduler's own read-then-check declines to reset a request whose status it
observes as owned or finished — see the next section for why that check is not what establishes the
property), and the store conformance arm
`AwaitingKeyDestruction_ShouldBeListable_NotRescheduled_AndNotCancellable`.

*(The second parenthetical previously read "no reset of an owned or finished request". That is the property
the system needs and it is not what the arm measures: its store is a fake whose status read returns one
fixed value for every call, so the status cannot change between the guard's read and its write and the
interleaving that defeats the guard is unrepresentable in the fixture. The arm is correct about the check;
the sentence was wrong about the system.)*

### A claimed erasure is never re-offered, so two instances cannot run one erasure

**SAFETY: a request that an executor has claimed, or that has reached a terminal outcome, cannot be put
back to `Scheduled`.** `Scheduled` is the only state the execution claim succeeds from, so writing it is
not the recording of an outcome — it is a grant of permission to the next caller. Writing it over a claimed
request re-offers an erasure somebody is already executing, and the second caller's claim then succeeds,
because it is not a second claim at all. Both callers destroy the subject's keys and both sign a completion
certificate, one of them attesting destruction its own pass found nothing left to perform. A signed
certificate is evidence a consumer hands an auditor, so the false one is an authenticated false statement.

**How it is achieved.** The write of `Scheduled` is conditional on the state the store finds, in one
indivisible operation, exactly as the claim is: `InMemoryErasureStore.UpdateStatusAsync` (a
compare-and-swap loop against the observed status, refusing the owned and terminal set), and a predicate on
the single `UPDATE` in `PostgresErasureStore.UpdateStatusAsync` and `SqlServerErasureStore.UpdateStatusAsync`.
Refused states are `InProgress` and `AwaitingKeyDestruction` (owned — an executor is mid-run, or the
key-management provider holds keys it has irreversibly scheduled) and `Completed` and `Cancelled`
(terminal). `Pending`, `Scheduled`, `BlockedByLegalHold`, `Failed` and `PartiallyCompleted` remain
resettable, which is how a transient failure is retried at all.

**LIVENESS: a request that failed on a transient fault is still resettable, so it is retried rather than
left terminal.** A store that refused every reset would satisfy the safety half completely and make every
transient failure permanent.

**Why the caller's own check does not establish the safety half.** The scheduler's retry path reads the
current status and declines to reset an owned request. That read and the subsequent write are two
operations, so a claim landing between them is invisible to it: the check narrows the window and cannot
close it. The store's refusal is the authority, and the retry path now treats a `false` return as "another
instance owns this" and stops.

**Evidence.** `ErasureStoreConformanceTestKit.UpdateStatusAsync_ResetToScheduledOfAClaimedRequest_ShouldBeRefused`
binds the safety half for every provider, with
`UpdateStatusAsync_ResetToScheduledOfAFailedRequest_ShouldSucceed` as its liveness twin. Both arms assert
the returned value AND the resulting status, because a store that reports a refusal and writes anyway is
the worst of the three outcomes. RED-proven on all three providers by removing the predicate: each reports
`returned True and left the status Scheduled`.

**Consumer obligation.** A custom `IErasureStore` must make the write of `Scheduled` conditional. The
contract on `UpdateStatusAsync` states the partition — a status is unconditionally writable only if it is
terminal or blocked — and the two conformance arms above fail a store that ignores it.

**And a cancelled erasure is never enrolled in key destruction.** `AwaitingKeyDestruction` is the
completion pass's discovery predicate, so writing it is not the recording of an outcome either — it enrolls
the request in a pass that asks the key-management provider to confirm destruction and then completes and
certifies it. That write now succeeds only from `InProgress` or from `AwaitingKeyDestruction` itself, so a
cancelled request cannot be enrolled and certified as erased. The source set includes the target because
the completion pass rewrites the detail while the provider still holds the keys; permitting only
`InProgress` would break that pass, which is why the liveness arm asserts the rewrite.
Bound by `UpdateStatusAsync_ResetToAwaitingKeyDestructionOfACancelledRequest_ShouldBeRefused` with
`UpdateStatusAsync_AwaitingKeyDestructionFromTheClaimedWindow_ShouldSucceed` as its twin, RED-proven on
all three providers in both directions: removing the guard lets a cancelled request be enrolled, and
narrowing it to `InProgress` only breaks the completion pass's rewrite.

**And an erasure interrupted mid-flight records a terminal outcome rather than staying claimed.** The
write that records what happened runs on a bounded token of its own, never the caller's. A graceful
shutdown cancels the caller's token before the execution handler runs, and both SQL stores open their
connection with `OpenAsync(cancellationToken)`, so the outcome write previously threw instead of landing
and the request stayed claimed with no status, no error and no certificate. Neither a throw nor a refusal
escapes that write: an exception raised there would replace the original fault, and a refusal is reported
rather than discarded, both through a Critical log naming the request. Bound by
`AnInterruptedErasureRecordsItsOutcomeShould.RecordATerminalStatusWhenTheHostCancelsMidExecution`,
RED-proven — restoring the caller's token to that write makes the cancellation escape the execution
instead of being recorded.

**A LAPSED legal hold does not block an erasure, on any provider, with or without the expiration sweep
registered.** In force is the conjunction of two independent facts: whether the hold was RELEASED (an act,
with an actor, a time and a reason) and whether its stated period has LAPSED (the passage of time). Neither
implies the other and neither is a proxy for the other. Both SQL providers previously filtered on the
release flag alone in all three in-force statements, so a hold whose stated expiry had passed was reported
in force forever and refused an erasure that should have proceeded — while the in-memory reference store
derived expiry correctly, which is the direction that hides, because anything built and unit-tested against
the reference store was green in every test and inert on both shipped providers. The instant is read once
per call and passed as a parameter, so one answer cannot straddle an expiry boundary mid-statement. Bound
by `ActiveHoldQueries_ShouldExcludeALapsedHoldAndStillReturnIndefiniteAndFutureOnes` in the shared kit,
which carries three discriminating rows per member — a past expiry that must be absent, a null expiry that
must be present because an indefinite hold is the commonest kind and without it a provider returning
nothing passes the safety half, and a future expiry that must be present because that is what catches an
inverted comparison. Measured RED on real PostgreSQL and real SQL Server before the fix and green after,
with the in-memory store green throughout as the control.

**THE INSTANT IS PART OF THE CONTRACT, SO THE ANSWER IS DECIDABLE.** The three in-force members take the
instant they are asked about and read no clock of their own. "The holds in force at `asOf`" is decidable;
"the holds in force at a time the store chooses" is not, so two sound implementations could disagree and
neither would be wrong. Two consequences follow for callers that must agree with themselves:

- The erasure gate reads subject-specific holds and tenant-wide holds and UNIONS them. One instant is
  pinned before both reads, so the union is one coherent answer rather than two sets that were never
  simultaneously true — which is what two independent clock reads straddling an expiry boundary produce.
- A caller may ask the same question twice and get the same answer, which is what makes a hold decision
  reproducible in an audit rather than merely repeatable in the same second.

Bound by `ActiveHoldQueries_ShouldDeriveInForceAgainstTheGivenInstant`, derived for all three stores. Its
fixture is one hold whose expiry is in the FUTURE, so it has not lapsed in real time and the two halves
differ only in the argument: asked as of an instant before the expiry it must be returned, asked as of one
after it must not. A provider reading its own clock returns it in both. Mutants RUN rather than recorded —
replacing the parameter with the provider's own clock reddens the arm on all three stores, and separately
for the listing member on each SQL provider, five mutants in all against green baselines.

**The complementary sweep keeps its own clock, deliberately.** `GetExpiredHoldsAsync` asks which holds
have lapsed while still unreleased — the opposite question — and it is the thing that ACTS on the passage
of time rather than a thing asked about a moment. A member's predicate follows from the question it asks,
which is why "add an expiry term everywhere" would be wrong by this contract.

**A REFUSAL IS REPORTED AS A REFUSAL, AND IT SPENDS NO FAULT BUDGET.** An execution result names its
outcome rather than carrying a single success flag, and five returns that were reported as failures are
refusals: the erasure was never attempted, so nothing was destroyed and the request is unchanged. A legal
hold deferring an erasure is the one a consumer sees — it was told the erasure FAILED when the law
required the data kept.

The behavioural half is that the scheduler reads the outcome. A refusal routed into the failure path
increments a per-process retry counter and, at exhaustion, writes a terminal failed status that the
scheduler's own query never returns and no pass enumerates — so a correct withholding became a permanent
non-erasure within the configured attempt limit. Bound by
`ErasureSchedulerBackgroundServiceShould.Not_spend_the_fault_budget_on_a_refusal_the_executor_never_attempted`,
whose fixture is deliberately a request that is STILL SCHEDULED: the failure path already exempts the
deferred status and the owned-or-terminal set by reading the request back, so a refusal in any of those
states would pass the arm whether or not the outcome is consulted. RED-proven by disabling the outcome
branch, which reddens that arm and no other.

A status re-read for the deferred case is retained alongside it, because the failure path is also
reachable from the exception path rather than only from a returned result.

**A lawful deferral is recorded as a deferral, never as a failure.** A request a legal hold defers is
exempt from the retry and failure path, so it neither spends the request's fault budget on a state no
retry can change nor has its status overwritten by a terminal failure. The terminal "failed after N
attempts" record it used to receive is indistinguishable from a genuine execution failure — a key provider
outage, a broken store — and overwrote the only durable statement of WHY, leaving the hold's lawful basis
and case reference inside an error string. Bound by
`ErasureSchedulerBackgroundServiceShould.Neither_retry_nor_fail_a_request_a_legal_hold_has_deferred`, with
`Handle_failed_erasure_execution` as its liveness twin: without that twin the exemption would be satisfied
by a scheduler that stopped marking anything failed at all.

**A DEFERRED ERASURE BECOMES EXECUTABLE AGAIN ONCE NO HOLD BLOCKS IT, which is the liveness half of a
lawful deferral.** A legal hold DEFERS an erasure rather than failing it, so a deferred request is not
finished and the controller's obligation to erase is not discharged. `ErasureHoldReevaluationProcessor`
reads every deferred request estate-wide through `IErasureBackgroundQueryStore.GetHoldDeferredRequestsAsync`,
re-reads the hold store under each request's own recorded tenant, and returns the ones no hold blocks to
`Scheduled`. The scheduler runs it before its execution pass, so a request it resumes is picked up in the
same poll. **The reportable property is that the request becomes EXECUTABLE again; the statutory clock is
the consumer's and this framework holds no authority over it.**

A partial hold leaves the request deferred exactly as a full one does, because neither mechanism the
executor runs can exclude a category from destruction — the per-subject key is scoped to the subject and
the event-store pass is whole-aggregate — so resuming on a partial hold would hand the executor a request
it must immediately refuse.

**The pass grants ELIGIBILITY, never permission to erase, and that is what keeps it cheap to get wrong.**
It writes the scheduled status and nothing else. Safety rests on the executor's run-level and per-key hold
checks, so the pass must never acquire a shortcut: no writing the claimed status itself, no bypassing the
execution entry point, no widening its accepted-status gate. A hold arriving between discovery and
execution costs one wasted claim cycle — liveness, never safety.

**Its liveness DERIVES from the hold-expiry parity above rather than assuming it.** While a lapsed hold was
reported as in force forever on both SQL providers, this pass would have been green in every unit test
against the in-memory reference store and INERT on both shipped providers: it would have re-read the hold,
been told it still blocked, and deferred the request again on every poll, permanently.

Bound by `ADeferredErasureIsReturnedToTheQueueShould` — five arms: the liveness arm, the still-held arm,
the partial-hold arm, an arm asserting a claimed request is left alone, and an END-TO-END arm that blocks
an erasure by a hold, releases the hold, runs the pass and asserts the erasure actually COMPLETES. That
last arm is the one that distinguishes this guarantee from the weaker claim it would be easy to make:
becoming executable again is not the property a data subject is owed; being erased is. Each is RED-proven: dropping
the partial-hold half of the check, dropping the hold check entirely, resuming to the claimed status
instead of the scheduled one, and discovering by the sibling member's status each redden a named arm.
The estate-wide query itself is bound across all three stores by one parameterised conformance arm,
`ErasureStoreConformanceTestKit.BackgroundDiscovery_AcrossOwners_ShouldReturnEveryRowOfItsOwnStatusUnscoped`,
which runs multi-tenant with two owning tenants under no ambient scope — the shape that detects an
ambient-tenant term, whose effect would be an empty discovery set and work silently never done.

**Scope bound, stated rather than implied: this covers the STORED deferrals only.** A request refused at
filing time is refused synchronously and never persisted, so no discovery query can find it by
construction — the caller holds a result whose request ID has no stored request. That is defensible,
since nothing was accepted, but it means "every deferred erasure becomes executable again" is true of the
stored half and vacuous for the other, and a reader should not infer the wider claim.

**Known gaps, and the rung.** This closes the re-offer path, the cancelled-enrolment path, the
interrupted-outcome path, the lapsed-hold path, the dishonest-deferral path and the deferred-liveness
path. It does not make the erasure lifecycle's liveness complete, and one gap is open by behaviour:
- A request CLAIMED by a process that then stops — a crash, an eviction or a scale-in — stays claimed
  permanently. No query enumerates it, the executor refuses it, and the claim carries no lease, owner
  identity or expiry by which another instance could establish that the owner is gone. Keys may already
  be destroyed. A graceful shutdown now records a terminal outcome, so that variant is visible; the crash
  variant is not, and a terminal status is not re-enumerated by any discovery query either, so neither
  variant is re-executable without an operator or a leased claim.

**TWO INVARIANTS THAT NOTHING STATED, and both are cheap to record now and unrecoverable later.** Each was
found by an adversarial contract review of the deferral design rather than by a test, and each is a
property the current code HAS and nothing protects.

1. **No irreversible act may occur between the execution claim and the hold re-check that follows it.**
   The claim is at `ErasureService.cs:541` and the re-check at `:552`; every destructive step — the key
   queue, the contributors, the event-store pass — is inside the block that begins after `:587`. This is
   what makes returning a hold-deferred request to an executable state safe: a deferred request performed
   no irreversible work, so re-entry is idempotent. **The day a key destruction moves above that
   re-check, returning a deferred request to the executable state silently becomes a double destruction,
   and the re-evaluation that returns it is the mechanism.** The ordering is the invariant, not the line
   numbers.

2. **Safety against erasing under a hold is discharged by the EXECUTOR, never by whatever re-evaluates a
   deferral.** The run-level check at `ErasureService.cs:552` and the per-key check immediately before
   each irreversible destruction at `:1262` are the two places that refuse. A pass that returns a
   deferred request to an executable state therefore grants ELIGIBILITY and not permission: a hold
   arriving in that window is caught by those checks, the request returns to the deferred state, and the
   cost is one wasted cycle — liveness, never safety. **The corollary binds whoever writes that pass: it
   must never take a shortcut past the executor** — not by writing the claimed status itself, not by
   widening the executor's accepted-status gate, not by bypassing it. Those would move the safety
   decision to a place that carries no safety obligation.

**AND A THIRD, OWED RATHER THAN HELD, recorded because the lease design turns on it.** The claim is a
genuine atomic claim — one conditional UPDATE with the expected state in its own WHERE clause — but it
prevents only a second CLAIM. Three writes that advance a request's attestation are today unconditional
on ownership: the completion record, the terminal status write, and the staging and destruction records,
which are keyed by request alone. With one executor that is sound and is documented as deliberate. **With
two it is not, and the harm is entirely in the attestation rather than in the destruction** — key
destruction is idempotent by construction, which is why the provider answers a self-destroyed key and a
never-held key identically. So any mechanism that can hand one request to a second executor owes a fence
on every attestation write, not only on the claim. Until then, the single-executor property rests on an
invariant nothing states: **the destroyed-handle set read when a request is claimed is complete with
respect to every destruction that request has performed.**

By the partition rule above `Pending` is also an enabling status — cancellation succeeds because a request
is `Pending` or `Scheduled` — and its write is not yet conditional. Nothing in this framework writes
`Pending` after creation, so no in-box path reaches the bad state; a caller of `UpdateStatusAsync` could.

This subsystem's blast radius is catastrophic and its rigor floor is R4; **R3 and R4 remain UNVERIFIED** —
the arms above are example-based, and no model checks the status machine's reachable states. The two gaps
above are exactly the shape a model would find. Each provider's own answer is
locked beside it: `InMemoryKeyManagementProviderShould` and the `IsKeyDestroyedAsync_*` arms of
`VaultKeyProviderShould` (a held key is not destroyed; an absent key is; a vault that cannot be asked throws
rather than answering; the read bypasses the metadata cache).

### A changed data-subject hashing key is refused, not silently absorbed

**Guarantee.** The durable **erasure store** (`PostgresErasureStore`, `SqlServerErasureStore` — which is
also the key-mint ledger, the key-destruction ledger and the certificate store) records a non-reversible
fingerprint of the data-subject token function the first time it is used. The record is **write-once**. A
process whose own token function fingerprints differently **cannot complete initialisation of that store**,
and therefore cannot read or write any token-keyed row in it.

**Scope of that guarantee, stated precisely rather than generously.** The legal-hold store and the
data-inventory store are registered separately and **each take their own connection string**, so they may
sit on different databases from the erasure store. They do **not** yet carry this record. Therefore:

| deployment | covered? |
|---|---|
| one database for all compliance stores (the common case, and the shipped default shape) | **yes** — the erasure store's check refuses startup before any token-keyed row is touched, and there is one hashing key per process |
| erasure store on database A, legal-hold or inventory on database B | **partially** — a changed key is still caught by the erasure store, but a database holding *only* holds or inventory rows has no fingerprint of its own |

The second row is a real gap and is listed under *Known gaps* below. It is narrower than it looks — one
process has one token function, so the erasure store's refusal stops that process entirely — but it is not
nothing: a host that registers only the legal-hold store, with no erasure store at all, performs no check.

**Why this is a guarantee and not a convenience.** Every durable record here — erasure requests, the
key-destruction ledger, the key-mint ledger an erasure enumerates from, legal holds, the data inventory —
is keyed by a pseudonymisation token derived from a data-subject identifier under a secret key. Change
that key and every key changes at once, with four consequences and **no error on any of them**:

| what happens | why nothing reports it |
|---|---|
| the mint-ledger enumeration returns empty | empty is a legitimate answer; it means "no personal fields were written" |
| the erasure destroys only what it can derive, then **signs a completion certificate** | the certificate attests the pass that ran, and that pass did complete |
| a **legal-hold lookup returns empty**, so the erasure proceeds over data the law requires kept | an absent hold is indistinguishable from no hold |
| personal data already encrypted stops being decryptable | a null field read is this framework's own assertion of a lawful erasure |

**Ordinary quarterly secret-rotation hygiene applied to the hashing key is sufficient to cause all four.**
A regulated consumer is left holding a signed erasure certificate for data that is still readable, and a
destroyed record that the law required them to retain.

**How it is achieved.** The fingerprint is derived by hashing one fixed canary identifier through the
*registered* hasher and running the result through HKDF
(`Erasure/DataSubjectPepperFingerprint.cs`), then claimed through
`IDataSubjectTokenFingerprintLedger.ClaimTokenFingerprintAsync` — an atomic insert-if-absent followed by a
read of what is now recorded. The comparison runs as the last step of each store's initialisation funnel
(`PostgresErasureStore.InitializeCoreAsync`, `SqlServerErasureStore.InitializeCoreAsync`), which every one
of that store's operations already awaits, so *every token-bound statement happens-after the check* by
construction rather than by review.

**It fingerprints BEHAVIOUR, not configuration**, because this interface is public and registered
first-one-wins: a consumer's own hasher can be the one producing tokens while the configured key is read
by nobody. Fingerprinting output therefore also detects a *replaced implementation*, which a configuration
check cannot see at all.

**Write-once is enforced at the database**, by a trigger the provisioning scripts install, not merely by
this framework declining to offer an update path. Initialization verifies an enabled trigger on the
fingerprint table covering both UPDATE and DELETE. SQL Server replaces its runtime-provisioned guard
inside one transaction, preserving the previous guard if replacement fails. These catalog checks verify
the required trigger shape; they do not prove arbitrary replacement trigger bodies. Use the shipped
provisioning scripts and keep privileged DDL access restricted. The guard rejects ordinary UPDATE and
DELETE; a trusted database owner can still disable/drop it or use administrative operations such as
TRUNCATE. Missing protection plus external mutation could let a process holding the wrong key replace
the fingerprint while an already-initialized process continues using the original token function.

**Initialization owns its durability boundary.** Schema verification/provisioning and fingerprint
claims execute independently of a caller's ambient business transaction. A later caller rollback must
not remove the fingerprint while leaving the process's initialization latch satisfied. Both PostgreSQL
and SQL Server suppress ambient enlistment only during initialization and fingerprint claims; ordinary
store operations retain their existing transaction participation. The caller's ambient context is restored
on success, failure and cancellation.

**The fingerprint carries no tenant term, and its table has no tenant column.** The token function belongs
to the process, not to a tenant. A per-tenant fingerprint would be worse than none: a tenant onboarded
after a rotation would record the new key as correct while every earlier tenant's records were already
orphaned, and the check would then pass forever on exactly the deployment where the damage is worst.

**Evidence.**
`PostgresTokenFingerprintShould` and `SqlServerTokenFingerprintShould` bind this against real database
instances and are never skipped — the properties under test are substrate behaviour that no mock
reproduces. Each suite proves the refusal, its liveness counterpart, the database-enforced write-once,
that exactly one of two concurrently starting processes with different keys may proceed, and that the
recorded value does not vary with the ambient tenant.
`TokenBoundStoreOperationsAwaitTheFunnelTests` asserts structurally that every public operation of such a
store awaits the funnel, so an operation added later that skips the check fails the build rather than
shipping.
`DataSubjectPepperFingerprintShould` pins the derivation against vectors computed by an independent
RFC 5869 implementation, and proves two hashers with the *same* key but different constructions
fingerprint differently.

**Consumer obligation.** Treat the hashing key as a **permanent derivation parameter for the life of the
data it protects**, not as a credential to rotate. If it must change, migrate every token-keyed record
before the change: afterwards the old token cannot be recomputed from anything that remains.

**Known gap — this covers RECORDS, not key NAMES.** A per-subject encryption key is named by a handle in a
key vault, which has nowhere to hold a fingerprint beside it. So personal data already encrypted under a
handle derived from the old token function remains undecryptable, and no check reports it. A consumer told
"a changed hashing key is detected" and given "detected for records only" has been misled by omission,
which is why it is stated here. A purely in-memory store records no fingerprint either, and needs none:
its records do not outlive the process, so no later process can be orphaned by them.

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

- **Remove a retention declaration by WITHDRAWING it first, then deploying.** Deleting a declaration
  from your code without an administrative withdrawal leaves the deployment's record holding it, so every
  instance holds fewer declarations than the record and **all erasure for that tenant stops — permanently,
  with no deploy that can fix it**, because the record only grows. The withdrawal is the one supported way
  to shrink it. This obligation is easy to reach by editing options, which is why it is stated first.
- **Never re-declare a pair that has been withdrawn.** A withdrawal tombstones the pair and the tombstone
  occupies the key, so a later declaration of the same `(tenant, aggregate type)` is silently dropped and
  the record keeps reading as withdrawn. A new obligation over the same data takes a NEW aggregate type
  name. Without this, a deliberate re-declaration and a stale instance are indistinguishable in every
  readable variable, and the two call for opposite actions.
- **Quiesce erasure execution across a deploy that ADDS a retention declaration.** Declarations live in
  your code and are read into each instance's own registry at construction, so during a rolling deploy an
  instance running the previous version holds a smaller set and will erase a newly-retained type as though
  no declaration existed — irreversibly, and certified Completed, because by its own view it was right.
  Stop the erasure scheduler across such a deploy, or ensure every instance holds the new declaration
  before any instance serves. Adding a declaration is the only trigger: removing one, or deploying
  anything else, is not. A single-instance deployment cannot reach it. **This obligation survives the
  deployment-wide record described in the guarantee above**, which narrows the window and cannot close it.

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
  `DeriveDifferentHandles_ForTenantAndSubjectSplitsThatConcatenateIdentically` binds the unambiguous segment
  composition against the collision plain concatenation would introduce, and
  `ATenantQualifiedSubjectKeyHandleShould.RefuseAnUnresolvedOwner` binds an unresolvable owner as
  INEXPRESSIBLE rather than defaulted -- a `null`, empty or whitespace tenant throws instead of folding to
  a shared handle -- and `ATenantQualifiedSubjectKeyHandleShould.KeepDefaultAndUntenantedOwnersIndependent`
  binds the default tenant and the untenanted sentinel as SEPARATE owners, so erasing one leaves the
  other's ciphertext decryptable. Measured, not predicted: a derivation that
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
