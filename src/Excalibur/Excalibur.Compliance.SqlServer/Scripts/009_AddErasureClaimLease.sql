-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
--
-- Moves CUSTODY of an erasure execution out of the status column and into columns of its own.
--
-- WHY THESE COLUMNS EXIST. Claiming an erasure for execution is already a genuine atomic claim: one
-- conditional UPDATE that moves the row from scheduled to in-progress with the expected state inside its
-- own WHERE clause, so exactly one caller of N wins. What the claim does NOT carry is any record of WHO
-- holds it or FOR HOW LONG, and that absence has two consequences.
--
-- First, an execution interrupted by a host shutdown stays in-progress forever. Every query that looks
-- for work to do correctly excludes an in-progress row -- it is, as far as the schema can say, being
-- worked on -- so the request is never offered to anybody again. Nothing throws and nothing is logged.
-- The subject's data is not erased, the request is not failed, and no operator has a row to look at.
--
-- Second, and the reason this is not merely a liveness problem: the status column is being asked to
-- carry two independent facts at once. OUTCOME is what happened to the erasure. CUSTODY is whether some
-- process is currently responsible for it. Encoding custody in the outcome axis is why there is no way
-- to say "nobody is working on this and nothing has happened to it yet" about a row that was once
-- claimed. These two columns give custody its own axis and leave the outcome axis alone -- deliberately
-- adding NO new status value, because a new status would put custody back where the defect is.
--
-- THE IDENTITY IS OPAQUE AND IS NOT A SEQUENCE. It exists to answer one question -- "is this write
-- coming from the claim that is currently held?" -- by equality and nothing else. It is NOT a fencing
-- token: a fencing token orders successive tenures against a resource shared by many rows, such as a
-- leader writing a shared log, and the resource fenced here is ONE REQUEST ROW and the attestation it
-- carries on that row -- its status, its completion, its certificate reference. Ordering is decided by
-- the store, because a claim is a single conditional UPDATE and the store picks which of two contenders
-- wins. The moment anything compares two identities for order, the tenure half has been reinvented, and
-- reinvented badly: there would be a counter to persist, to hand out monotonically, and to exhaust,
-- buying nothing this equality check does not already give.
--
-- THE DESTRUCTION LEDGER AND THE STAGED INTENTS ARE DELIBERATELY OUTSIDE THE FENCE, and this is stated
-- rather than left to inference, because an absence here is indistinguishable from an oversight and the
-- next reader's instinct will be to finish the job. They record an irreversible act that has ALREADY
-- happened. Refusing a ledger write because the writer's claim was superseded destroys the only evidence
-- that the material is gone: the generation cannot be read back, because the destruction took it, so no
-- successor can record it either and the subject's erasure becomes permanently unreportable. The ledger
-- is keyed on the generation, which is minted once and never reused, so two claims recording the same
-- generation produce one row idempotently -- that sharing is not a defect, it is how a retried erasure
-- attests what an earlier pass achieved. An earlier version of this comment described the fenced
-- resource as "the attestation tables keyed to its request", which read as authority for fencing them.
--
-- A LAPSE DOES NOT REVOKE; A RE-CLAIM REVOKES. The expiry term belongs in the CLAIM's own predicate and
-- in the queries that discover work, and NOWHERE ELSE -- in particular never in a fenced write. The
-- index below makes "AND claim_expires_at > now()" look like a natural addition to the completion and
-- status statements, and adding it there is the opposite of safe: an executor whose run outlasted its
-- lease but whom nobody superseded would be refused its own completion, leaving the keys destroyed, the
-- erasure performed, and no certificate ever issued. A fenced write compares the identity and only the
-- identity. A superseded holder learns it lost the request when its first fenced write is refused, which
-- is after it may already have done destructive key work -- which is precisely why the ledger above must
-- stay unfenced.
--
-- IMPLEMENTATIONS MUST NOT TRUNCATE A PRESENTED IDENTITY BEFORE COMPARING IT. The column is 64
-- characters wide; a parameter typed to that width would silently make a 65-character presented value
-- match a 64-character stored one. Only the store mints an identity, so the insert side cannot produce
-- an over-long value; the compare side is where a bound parameter would cut one.
--
-- Binary collation on the identity, matching this schema's tenant columns and for the same reason: the
-- framework compares it ordinally, and a database collation more permissive than the framework's
-- comparison would admit a write the fence was supposed to refuse.
--
-- THE EXPIRY IS WRITTEN AND COMPARED BY THE SERVER, NEVER BY THE CLAIMING PROCESS. It is derived from
-- SYSUTCDATETIME() at the database and every lapse test compares against SYSUTCDATETIME() at the
-- database. A process-side instant passed in as a parameter would make the lapse a comparison between
-- two machines' clocks, which is the one comparison that may never decide anything: a claimant whose
-- clock runs fast would hand itself a lease that has already expired, and one whose clock runs slow
-- would hold a lease nobody else can take. The existing code passes a process-side instant into the
-- status CAS, which is harmless for a grace period and is not harmless for a lease.
--
-- Every statement is guarded, so the script is safe to re-run and safe to apply to a database that
-- already has part of it.

-- ---------------------------------------------------------------------------------------------------
-- THE TWO CUSTODY COLUMNS.
--
-- Both NULLABLE, and null is the normal resting state: a scheduled request that nobody has claimed, and
-- a request whose work is finished, both hold null in both columns. That is why neither carries a
-- default. A default would have to assert either that every existing row is claimed or that every
-- existing row's claim expired at some instant nobody measured, and both are fabrications about rows
-- written before custody was a thing this schema tracked. An unclaimed row is the honest reading of
-- every row that exists today, and it is also the correct one: no process is holding any of them.
--
-- They are added to the requests table rather than to a table of their own because custody is a
-- property of the request, it is read on the same statement that reads the status, and it is written by
-- the same single UPDATE that grants the claim. A separate table would make the claim two writes, and
-- two writes are not an atomic claim.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[compliance].[ErasureRequests]') AND name = N'ClaimIdentity')
BEGIN
    ALTER TABLE [compliance].[ErasureRequests]
        ADD ClaimIdentity NVARCHAR(64) COLLATE Latin1_General_BIN2 NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[compliance].[ErasureRequests]') AND name = N'ClaimExpiresAt')
BEGIN
    ALTER TABLE [compliance].[ErasureRequests]
        ADD ClaimExpiresAt DATETIMEOFFSET NULL;
END
GO

-- ---------------------------------------------------------------------------------------------------
-- THE TWO COLUMNS ARE ALL-NULL OR ALL-NON-NULL, ENFORCED BY THE DATABASE.
--
-- An identity with no expiry is a claim that can never lapse, which is the defect this migration exists
-- to remove, reintroduced one column at a time. An expiry with no identity is a lapse test that no write
-- can ever be fenced against. Neither half is meaningful alone, so neither half may exist alone, and
-- stating that as a constraint rather than as a convention is what makes a provider that writes only one
-- of them fail loudly instead of silently producing an unfenceable row.
--
-- This is the same shape as the ownership column group elsewhere in this product, and it is stated here
-- rather than inherited, because a check constraint is the only thing that holds when a future statement
-- is written by somebody who has not read this comment.

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_ErasureRequests_ClaimPairing'
      AND parent_object_id = OBJECT_ID(N'[compliance].[ErasureRequests]'))
BEGIN
    ALTER TABLE [compliance].[ErasureRequests]
        ADD CONSTRAINT CK_ErasureRequests_ClaimPairing
        CHECK (
            (ClaimIdentity IS NULL AND ClaimExpiresAt IS NULL)
            OR (ClaimIdentity IS NOT NULL AND ClaimExpiresAt IS NOT NULL)
        );
END
GO

-- ---------------------------------------------------------------------------------------------------
-- AN INDEX FOR THE LAPSE PREDICATE, AND DELIBERATELY NOT A FILTERED ONE.
--
-- TWO queries that find work to do gain a lapse term, not one: the scheduled-request query becomes
-- "scheduled, OR claimed with an expiry now in the past", and the awaiting-key-destruction query needs
-- the same term once the confirmation pass takes a claim of its own -- without it two confirmers run
-- concurrently and the second is refused at its first write on every poll, forever. This index serves
-- both. The
-- second disjunct is new, and the rows that matter are exactly those with a non-null expiry, so a
-- FILTERED index on `ClaimExpiresAt IS NOT NULL` is the textbook shape and is what this script carried
-- first. It was removed, and the reason is a correctness hazard rather than a preference.
--
-- MEASURED, not reasoned: with a filtered index present, SQL Server refuses ALL data modification on
-- this table from any connection whose QUOTED_IDENTIFIER is OFF -- `Msg 1934 ... INSERT failed because
-- the following SET options have incorrect settings: 'QUOTED_IDENTIFIER'`. The same INSERT succeeds
-- immediately with the option ON. The modern .NET client sets it ON, so the framework's own writes are
-- fine; a consumer's command-line tool, an older driver, a linked server or a legacy session is not,
-- and the symptom is that erasure requests cannot be written AT ALL.
--
-- That is a dependability failure on the write path of a compliance subsystem, traded for a selectivity
-- gain on a query a background timer runs. A plain nonclustered index carries no SET-option requirement
-- and still answers the predicate; it simply also carries the rows holding NULL. That is the correct
-- trade here, and it is the whole reason this index is not filtered.
--
-- The PostgreSQL sibling of this script DOES use a partial index, and that asymmetry is deliberate
-- rather than drift: a partial index in PostgreSQL imposes no comparable requirement on writers.

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ErasureRequests_ClaimExpiresAt'
      AND object_id = OBJECT_ID(N'[compliance].[ErasureRequests]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ErasureRequests_ClaimExpiresAt
        ON [compliance].[ErasureRequests] (ClaimExpiresAt);
END
GO
