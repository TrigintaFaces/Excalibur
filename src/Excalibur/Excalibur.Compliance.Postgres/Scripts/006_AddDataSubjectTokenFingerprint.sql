-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
--
-- Records which data-subject token function produced this store's pseudonymisation tokens.
--
-- WHY THIS TABLE EXISTS. Every durable compliance record here is keyed by a pseudonymisation token
-- derived from a data-subject identifier under a secret key. Change that key -- ordinary quarterly
-- secret-rotation hygiene is enough -- and every key changes at once: an erasure enumerates no key
-- handles and signs a completion certificate over everything it missed, a legal-hold lookup returns
-- nothing so the erasure proceeds over data the law required kept, and already-encrypted personal data
-- stops being decryptable. None of it raises and none of it is logged. This table is what turns that
-- silence into a refusal to start.
--
-- Every statement is guarded, so the script is safe to re-run and safe to apply to a database that
-- already has part of it.

-- ---------------------------------------------------------------------------------------------------
-- The fingerprint. ONE row, for the whole database.
--
-- THERE IS DELIBERATELY NO TENANT COLUMN, AND ITS ABSENCE IS THE POINT.
-- The token function is a property of the process, not of a tenant, so there is no honest tenant term
-- to carry. A per-tenant fingerprint would be actively harmful: a tenant onboarded after a rotation
-- would record the new function as correct while every earlier tenant's records were already orphaned,
-- and the check would then pass forever on exactly the deployment where the damage is worst.
--
-- Omitting the column rather than merely declining to use it makes a tenant predicate INEXPRESSIBLE:
-- an attempt to add one fails loudly here instead of silently narrowing the one row to none. Do not
-- "fix" this table by adding a tenant column to match its siblings.
-- ---------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "compliance"."erasure_token_fingerprint" (
    -- A fixed constant, so the table holds at most one row by primary key rather than by convention.
    "id" TEXT NOT NULL,

    -- A non-reversible fingerprint of the token function, derived by hashing one fixed canary identifier
    -- through the registered hasher and running the result through a key-derivation function. It is NOT
    -- a data-subject token and NOT a secret: the canary is a framework constant, so this value grants no
    -- capability its reader did not already have.
    "fingerprint" TEXT NOT NULL,

    -- Audit only. Never read by any predicate.
    "recorded_at" TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    CONSTRAINT "pk_erasure_token_fingerprint" PRIMARY KEY ("id"),
    CONSTRAINT "ck_erasure_token_fingerprint_not_blank" CHECK (length(btrim("fingerprint")) > 0)
);

-- ---------------------------------------------------------------------------------------------------
-- WRITE-ONCE, ENFORCED HERE RATHER THAN TRUSTED.
--
-- The claim this table backs is an insert-if-absent followed by a read of what is now recorded. That
-- is only sound while a recorded fingerprint can never change. If any statement could update it, two
-- processes can interleave so that the one with the CORRECT token function refuses while the one with
-- the WRONG function proceeds:
--
--     A: INSERT -> wins, records fp1
--     B: UPDATE fingerprint := fp2
--     A: SELECT -> reads fp2 -> refuses, against its own correct function
--     B: SELECT -> reads fp2 -> matches -> PROCEEDS with the wrong one
--
-- The framework grants itself no update path. This trigger is what makes that true of the database as
-- well, including for an administrator at a psql prompt who believes they are repairing something.
-- ---------------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION "compliance"."erasure_token_fingerprint_is_write_once"()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'compliance.erasure_token_fingerprint is write-once: the recorded data-subject token fingerprint '
        'must never change. Changing it would let a process with the wrong hashing key proceed while a '
        'process with the right one is refused. If the hashing key genuinely changed, the records keyed '
        'under the old one are unreachable and must be migrated before any new fingerprint is recorded.';
END;
$$;

DROP TRIGGER IF EXISTS "trg_erasure_token_fingerprint_write_once"
    ON "compliance"."erasure_token_fingerprint";

CREATE TRIGGER "trg_erasure_token_fingerprint_write_once"
    BEFORE UPDATE OR DELETE ON "compliance"."erasure_token_fingerprint"
    FOR EACH ROW
    EXECUTE FUNCTION "compliance"."erasure_token_fingerprint_is_write_once"();
