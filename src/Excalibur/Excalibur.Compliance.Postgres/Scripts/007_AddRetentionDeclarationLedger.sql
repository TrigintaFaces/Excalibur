-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
--
-- Records which aggregate-retention declarations some process in this deployment has witnessed.
--
-- WHY THIS TABLE EXISTS. A retention declaration -- the statement that an aggregate type must survive an
-- erasure because the law requires the record kept -- lives in the APPLICATION'S CODE. Each instance
-- builds its own set in its own constructor and nothing compares one instance's set against another's.
-- So during a rolling deploy that ADDS a declaration, an instance running the previous version holds a
-- smaller set, and by its own view it is erasing a type with no retention: it irreversibly destroys the
-- key, the escrow and the recovery tokens for data the deploy declares retained, and certifies the
-- erasure Completed. Nothing raises and nothing is logged, because by that instance's view it was right.
--
-- This table is the deployment-wide record that lets an instance discover it is behind and withhold the
-- irreversible act. Everything else in this schema that governs destruction is already durable -- the
-- requests, the legal holds, the key ledgers, the token fingerprint. The declaration was the only input
-- to an irreversible act that existed solely as a local snapshot.
--
-- Every statement is guarded, so the script is safe to re-run and safe to apply to a database that
-- already has part of it.

-- ---------------------------------------------------------------------------------------------------
-- ONE ROW PER DECLARED (tenant, aggregate type) PAIR.
--
-- NOT ONE SET-VALUED ROW, and the difference is a lost update. A single row holding the whole set makes
-- recording a read-modify-write: two instances starting together each read the set, each write their own
-- union, and the first one's additions are gone -- which is exactly the state this table exists to
-- detect, manufactured by the table itself. Per-pair rows make the union form BY CONSTRUCTION: for each
-- pair exactly one insert wins and every other is a no-op, with no read in the write path at all.
--
-- THE TENANT IS IN THE PRIMARY KEY AND NOWHERE ELSE. The pair IS the identity of a row, so the key
-- addresses it completely. A tenant predicate added to a statement the key already addresses can only
-- ever turn the correct row into zero rows -- that is not hypothetical here, it is the mechanism that
-- once stopped the outbox marking messages it had claimed. The acceptance criterion for tenancy on this
-- table is "a provider that omits the tenant from the KEY fails", never "every statement carries a
-- predicate".
--
-- THERE IS NO WRITE-ONCE TRIGGER, and that is the difference from the token-fingerprint table rather
-- than an omission. A fingerprint must NEVER change, so write-once is its invariant. A declaration set
-- legitimately GROWS -- adding retentions over time is the supported workflow -- and a withdrawal must
-- be able to tombstone a row IN PLACE. The tombstone occupies the key, which is what stops a later
-- instance whose code still declares a withdrawn retention from resurrecting it.
--
-- BOTH KEY COLUMNS CARRY AN EXPLICIT DETERMINISTIC COLLATION. The framework compares both ordinally. On
-- a database whose default collation folds case, two distinct aggregate types would collapse onto one
-- row and the staleness comparison would report a declaration as recorded when the deployment had
-- recorded a different one -- a passing check over a false premise.
-- ---------------------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "compliance"."erasure_retention_declarations" (
    tenant_id VARCHAR(64) COLLATE "C" NOT NULL,
    aggregate_type VARCHAR(512) COLLATE "C" NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    -- A WITHDRAWAL TOMBSTONES RATHER THAN DELETES, so "this deployment withdrew the obligation" and
    -- "this deployment never recorded it" stay distinguishable to an auditor and to the comparison.
    -- All three withdrawal columns move together: a tombstone with no actor and no reason records that
    -- an obligation ended and nothing about the ground for ending it, which is the half an auditor
    -- asks for.
    withdrawn_at TIMESTAMPTZ NULL,
    withdrawn_by VARCHAR(256) NULL,
    withdrawal_reason VARCHAR(1000) NULL,

    CONSTRAINT pk_erasure_retention_declarations PRIMARY KEY (tenant_id, aggregate_type),
    CONSTRAINT ck_erasure_retention_declarations_type_not_blank
        CHECK (length(btrim(aggregate_type)) > 0),
    CONSTRAINT ck_erasure_retention_declarations_withdrawal_complete
        CHECK ((withdrawn_at IS NULL AND withdrawn_by IS NULL AND withdrawal_reason IS NULL)
            OR (withdrawn_at IS NOT NULL AND withdrawn_by IS NOT NULL AND withdrawal_reason IS NOT NULL))
);

-- ---------------------------------------------------------------------------------------------------
-- The read on the erasure path asks for one tenant's declarations, which the tenant's leading position
-- in the primary key already serves. This index is declared anyway because the framework's schema
-- verifier asks which columns a read locates rows by: a database whose key was provisioned in the other
-- order would otherwise pass verification while every read scanned the table.
-- ---------------------------------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_erasure_retention_declarations_tenant
    ON "compliance"."erasure_retention_declarations" (tenant_id);
