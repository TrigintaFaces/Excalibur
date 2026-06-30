# Excalibur.AuditLogging.Abstractions

Backend-agnostic audit-trail **integrity** abstractions for the Excalibur framework.

This package defines the single shared tamper-evidence contract consumed by every audit sink
(`Excalibur.AuditLogging`, `Excalibur.Data.ElasticSearch` security audit, …), so all backends provide the
same guarantee instead of each rolling its own scheme.

## Contents

- **`IAuditIntegrityStrategy`** — the keyed-MAC + hash-chain tamper-evidence contract. Operates on opaque
  canonical bytes so one implementation serves every backend. Keyed (HMAC), fail-closed on a missing key,
  constant-time verification, versioned tags (`v1:{keyId}:{mac}`).
- **`AuditRecordCanonicalizer`** — the deterministic, version-prefixed, length-prefixed (boundary-injective)
  canonicalization helper. Each backend canonicalizes its own record's integrity-covered fields through this
  before computing or verifying a tag.
- **`AuditChainLink` / `AuditChainVerificationResult`** — the chain-verification value types.
- **`IAuditSigningKeyProvider`** *(added in a follow-up)* — supplies the secret signing key, held outside the
  audit store.

## Design

- **Provider → abstraction.** Audit sinks depend on this package; this package depends on no sink.
- **Keyed and chained.** The MAC covers `canonicalize(record) ‖ priorTag`, so forging a record or
  inserting / deleting / reordering records is detectable without the key.
- **Verify live fields.** Verification re-canonicalizes the reloaded record's live fields — never a persisted
  canonical blob — so it checks the queryable record an attacker could tamper with.
