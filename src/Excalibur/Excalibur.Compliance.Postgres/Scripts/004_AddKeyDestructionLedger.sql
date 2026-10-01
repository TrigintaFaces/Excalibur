-- ===========================================================================
-- 004 — Key-destruction ledger and staging table
-- ===========================================================================
-- Run this ONLY on a database already provisioned by an earlier version of
-- 001_CreateComplianceSchema.sql. A fresh database gets the final shape from
-- 001 and must not run this script.
--
-- WHY A SEPARATE SCRIPT IS NEEDED AT ALL: every statement in 001 is guarded by
-- IF NOT EXISTS, so re-running it on a provisioned database is a no-op and the
-- new column never arrives. The store's startup schema check then FAILS, by
-- design, naming the missing column — loud, and at the cheapest possible
-- moment. This script is what makes that failure fixable.
--
-- WHAT IT DOES TO EXISTING ROWS, stated plainly because it is destructive and
-- the consequence is wider than "in-flight erasures":
--
-- Rows written by the earlier schema record a destruction by (request, handle)
-- and carry NO generation. A generation cannot be back-filled — the value only
-- ever existed inside the key backend, and the destruction annihilated it. A
-- row with no generation cannot answer the read predicate, so keeping it would
-- force the predicate back onto the handle, which is the defect this change
-- exists to remove. The table is therefore DROPPED AND RECREATED, and that
-- discards EVERY historical destruction record, not only in-flight ones.
--
-- SO, AFTER THIS SCRIPT RUNS: every past erasure request reports an EMPTY
-- destroyed-key-handle list. That list is read from this table, so a request
-- completed months ago will report zero destroyed handles where it previously
-- reported several.
--
-- WHAT SURVIVES, because this is what makes the trade acceptable rather than
-- alarming: this script touches ONLY this one table. The signed completion
-- certificate is untouched and remains the durable attestation for every
-- completed request — it carries the erasure summary, including the count of
-- keys destroyed, and it is stored as the exact signed bytes so its signature
-- still verifies. The request row is untouched too and keeps its own
-- keys-destroyed count. What is lost is the per-handle BREAKDOWN, which exists
-- to let an interrupted erasure attest what its earlier passes achieved, not to
-- serve as the attestation itself.
--
-- The acute case: an erasure that is MID-RETRY when you run this loses the
-- record of which handles its earlier passes destroyed, so it can no longer be
-- reported complete and will need re-filing. Let in-flight erasures finish
-- before applying this.
-- ===========================================================================

DO $$
BEGIN
    -- Idempotent: once key_generation exists this whole block is skipped, so
    -- re-running the script cannot drop a populated ledger a second time.
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'compliance'
          AND table_name   = 'erasure_destroyed_keys'
          AND column_name  = 'key_generation')
    THEN
        DROP TABLE IF EXISTS "compliance"."erasure_destroyed_keys";

        -- See 001 for the full rationale. In short: the primary key is the
        -- GENERATION alone, because a generation is minted once and never
        -- reused, so a row's existence IS the destruction statement and two
        -- rows for one generation are a contradiction the database refuses.
        -- request_id and key_handle are audit attributes, never key
        -- components — keying on the handle silently drops a second
        -- destruction at that handle.
        CREATE TABLE "compliance"."erasure_destroyed_keys" (
    key_generation TEXT COLLATE "C" NOT NULL PRIMARY KEY,
            -- NULLABLE, because a destruction the CONSUMER performed and asserted through the
            -- ledger's public write has no erasure request and no handle to name. Audit
            -- attributes either way: the read predicate names neither.
            request_id     UUID        NULL,
            key_handle     TEXT COLLATE "C" NULL,
            destroyed_at   TIMESTAMPTZ NOT NULL,
            -- WHO asserted the destruction: 'framework-erasure' for one this framework
            -- performed (staged before the destroy, recorded after it), 'caller-assertion' for
            -- one the consumer performed and recorded themselves, which nothing here
            -- re-verified. Stated EXPLICITLY rather than inferred from the nulls above --
            -- deriving a fact from a missing value is the reasoning this table exists to
            -- replace.
            recorded_by    TEXT        NOT NULL
        );

        CREATE INDEX ix_erasure_destroyed_keys_request
            ON "compliance"."erasure_destroyed_keys" (request_id, key_handle);
    END IF;
END $$;

-- The staging table, written BEFORE a destruction. A SEPARATE table from the
-- ledger, deliberately: between the stage and the destruction a staged row
-- names LIVE material, so nothing that resolves the destruction predicate may
-- be able to reach it. See 001 for the full rationale.
CREATE TABLE IF NOT EXISTS "compliance"."erasure_destruction_intents" (
    request_id     UUID        NOT NULL,
    key_handle     TEXT COLLATE "C" NOT NULL,
    key_generation TEXT COLLATE "C" NOT NULL,
    staged_at      TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (request_id, key_handle, key_generation)
);
