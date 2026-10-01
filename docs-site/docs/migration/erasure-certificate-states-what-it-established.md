---
title: An erasure certificate states only what the erasure established
sidebar_label: Erasure certificate claims
---

# An erasure certificate states only what the erasure established

The signed erasure certificate validated its envelope and trusted every claim inside it. Four things
change so that a claim nobody established is no longer presented to an auditor as one, and so that the
document is still always issued.

**Who is affected.** Hosts that read `ErasureException.Basis` off a certificate, that switch on
`ErasureRequestStatus`, or that implement `IErasureContributor` and return retained data. A host that
files erasures and reads `ErasureExecutionResult.Success` has nothing to change.

**Existing certificates still verify.** The canonical form a signature covers is unchanged for every
certificate whose legal basis was a defined value -- an enum serialises to its NAME, and the name is the
same before and after. No re-signing, no re-issuing. **Your DATABASE, however, does need a migration**
-- see section 1.

## 1. Both legal-basis enumerations gained a zero member, and every other member moved up by one

```diff
  public enum LegalHoldBasis
  {
+     NotEstablished = 0,
-     FreedomOfExpression = 0,
+     FreedomOfExpression = 1,
      ...
  }

  public enum ErasureLegalBasis
  {
+     NotEstablished = 0,
-     DataNoLongerNecessary = 0,
+     DataNoLongerNecessary = 1,
      ...
  }
```

**Why.** `required` makes omission inexpressible to the C# compiler, which is real protection. It does
nothing about a reflection binder, a deserializer, a store round trip, or an out-of-range cast — and
whatever sits at zero is what all of those produce. Zero was `FreedomOfExpression`, Article 17(3)(a), and
`DataNoLongerNecessary`, Article 17(1)(a). So a value nobody set was indistinguishable from a deliberate
claim, and on a signed certificate it read as the ground under which a tax record was kept.

Fixing the **type** rather than one property closes it for every place that holds one of these and every
place that will. A nullable property would have protected only the property it was applied to.

**An unestablished ground makes the erasure incomplete.** It is an unmet obligation, and presenting an
unmet obligation as a lawful basis turns a failure into a defensible retention. The certificate is still
issued and records that the erasure did not complete.

**Existing signed certificates still verify.** The canonical form serialises an enum by NAME, so
`"Basis":"LegalObligation"` is byte-identical before and after. Only a document carrying a value outside
the enumeration changes, and that document was already defective.

### You MUST run a database migration, and your application will refuse to start until you do

The ordinals are persisted as `INT`, so **every row written before this change holds a value that now names
a different legal basis.** Six columns across three tables are affected:

| table | column, before | column, after |
|---|---|---|
| `ErasureRequests` / `erasure_requests` | `LegalBasis` / `legal_basis` | `LegalBasisV2` / `legal_basis_v2` |
| `ErasureCertificates` / `erasure_certificates` | `LegalBasis` / `legal_basis` | `LegalBasisV2` / `legal_basis_v2` |
| `LegalHolds` / `legal_holds` | `Basis` / `basis` | `BasisV2` / `basis_v2` |

Apply the script your package ships:

- **SQL Server** — `scripts/003_ReplaceLegalBasisColumns.sql`
- **PostgreSQL** — `scripts/002_ReplaceLegalBasisColumns.sql`

**Pre-upgrade legal-basis values are NOT carried forward.** The migration replaces the column rather than
converting it, so after you run it every pre-upgrade row reports its legal basis as **not established**.

That is the honest outcome rather than a lossy one. The renumbering means an old ordinal cannot be read
through the new numbering without naming a different ground, and this framework has no record of which
version wrote which row. Converting the values would be a guess presented on a compliance record as a
measurement. The enumeration now has a member that says exactly what is true — nobody established this — and
that is where those rows land.

**The rest of each record is untouched.** Identifiers, timestamps, counts, signatures and every other claim
survive. Only the legal-basis ordinal is not carried across. A signed certificate's payload is stored
separately and in full, so **its signature still verifies**; this column is a projection of one claim, not
the source of it.

**The new column name is the protection.** The stores bind `LegalBasisV2` and `BasisV2`, so a database that
has not been migrated fails at startup naming the missing column — rather than starting happily and
misreading every stored legal basis. A loud error you fix in one step is the better failure; a misread legal
basis is a wrong ground on a signed record that nobody sees. A migration that merely *renamed* the column
would have kept the old values and produced exactly that silent misreading, which is why the column is
replaced.

**Running the script twice is safe.** Each block runs only if the OLD column is still present, and the block
drops that column — so the precondition is consumed by the act it guards. There is no flag to set and no
bookkeeping table to fall out of step with your schema. Each block is one transaction, so an interrupted run
leaves the old column intact, still readable by the previous package version and still migratable by running
the script again.

**If you store consent, its legal-basis column is NOT affected.** Consent records carry the Article 6 lawful
basis for processing, a different enumeration this change does not renumber. In PostgreSQL that column
happens to share the name `legal_basis`; the shipped script names its tables explicitly so it cannot touch
it, and you must not replace it either.

## 2. `ErasureException.RetentionPeriod` is no longer emitted

The property is unchanged in name and type and is now always `null`, meaning *the end of this retention is
not established*.

**Why.** A duration with no anchor reads as computable. "Six years" leaves the reader to supply the missing
instant, and the only instant in front of them is the certificate's own date -- which restarts a statutory
clock at the moment the data subject asked to be erased, and so extends every retention past the end the
law gives it. The certificate would be evidence for a longer retention than the obligation supports, in a
document produced to prove the opposite.

The end cannot yet be stated because it depends on facts the deployment holds and the framework does not: a
warranty term runs from delivery, not from the order that created the record.

**What to change.** A readout that printed the period should say the end is not established and point at
the declared obligation. What the entry does establish is unchanged: `Basis`, `DataCategory`, `Reason`, and
`RetainedKeyHandle` -- the ground, the data, the justification, and the key whose destruction ends the
retention.

## 3. A new terminal state: `ErasureRequestStatus.CompletedExceptConcurrentWrites` (9)

An erasure that did everything asked of it, and for which personal data of the same data subject was
written while it was running, is recorded in this state rather than as `Completed` or
`PartiallyCompleted`.

**Why.** Key destruction runs once, near the start. A write landing afterwards, for the same subject, mints
a live key at a handle the erasure had destroyed -- so at the moment the certificate is signed there is
personal data of an erased subject under a live key. With only the two existing states available that
outcome had to be reported as one of them: `Completed` attests an erasure that did not happen, and
`PartiallyCompleted` says something failed when nothing did.

**What to change.** A `switch` over `ErasureRequestStatus` gains a case. It is not a failure and not a
completion: nothing needs retrying for the data the erasure did reach, and the remedy is another erasure
for the same subject once the writes have stopped. Confidentiality is not affected -- data encrypted under
the destroyed key stays unreadable; what survives is data written after that destruction, which the erasure
never covered.

`ErasureExecutionResult.CompletedExceptConcurrentWrites(...)` reports it, with `Success` set to `false`.

## 4. Your key provider must be able to say whether a key is destroyed

Before the certificate is signed, the erasure re-asks the provider whether each handle it destroyed is
still destroyed. A provider that cannot answer leaves that state unmeasured, and an erasure that destroyed
keys and cannot re-establish their state is not recorded as `Completed`.

**Who is affected.** Only a host supplying its own `IKeyManagementProvider` that does not implement
`IKeyDestructionStatusProvider`. Every provider shipped with the framework implements it, and startup
validation already warned about a provider that does not.

**What to change.** Implement `IKeyDestructionStatusProvider` on your provider and answer for it from
`GetService(Type)`. A backend with no recovery window implements it trivially -- a key it does not hold is
destroyed -- which is a statement the provider makes rather than one the erasure assumes. An erasure
discharged entirely by record deletion destroys no keys and is unaffected.

## What did not change

- The signature scheme, its version (`2.0`), and the bytes it covers for an existing certificate.
- `ErasureCertificateVerifier.Verify`, which still checks the document exactly as issued.
- The certificate is always issued. Signing happens after the irreversible act, so a refusal would leave
  you with the destruction performed and no evidence that it was performed. What varies is what the
  document asserts, never whether you get one.
