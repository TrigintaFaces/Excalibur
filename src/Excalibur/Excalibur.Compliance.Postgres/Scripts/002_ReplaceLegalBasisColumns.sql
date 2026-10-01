-- PostgreSQL migration for Excalibur.Compliance.Postgres -- legal-basis column replacement
-- Version: 2.0
--
-- WHAT THIS DOES, AND WHY IT DISCARDS THE OLD VALUE RATHER THAN CONVERTING IT
--
-- The legal-basis enumerations gained a zero member meaning "no ground was established", and every other
-- member moved up by one. These values are persisted as INT, so every row written before that change holds
-- an ordinal that now names a DIFFERENT legal basis: a stored 1 that meant "legal obligation" would read
-- back as "freedom of expression".
--
-- This script REPLACES the column rather than renaming it, and the replacement is deliberately empty of
-- the old data. Pre-upgrade ordinals are NOT carried forward. Every existing row lands on 0, which is the
-- enumeration's "not established" member, and that is the honest value: this framework can no longer say
-- what ground the row was written under, and the enumeration now has a member that says exactly that.
--
-- A RENAME WOULD BE THE WRONG SHAPE HERE, and it is worth stating because it is the obvious first idea.
-- Renaming preserves the values, so the old ordinals would survive under the new column name and be read
-- through the new numbering -- which is precisely the silent misreading this migration exists to prevent.
-- Renaming is the right tool when the encoding is unchanged; it is the wrong tool when the encoding is the
-- thing that changed.
--
-- THE NEW COLUMN NAME IS THE PROTECTION. The store binds legal_basis_v2 and basis_v2, so a database that
-- has not run this script fails at startup naming the missing column, rather than starting and misreading
-- every stored basis. A loud error you fix in one step is the better failure.
--
-- RUN THIS ONCE, AFTER UPGRADING THE PACKAGE AND BEFORE STARTING THE APPLICATION.
--
-- RE-RUNNING IS SAFE. Each block runs only if the OLD column is still present, and the block drops that
-- column -- so the precondition is CONSUMED by the act it guards. A second run finds no old column and does
-- nothing. There is no flag to remember and no bookkeeping table that can fall out of step with the schema.
-- Each block is one transaction, so an interrupted run leaves the old column intact, still readable by the
-- previous package version and still migratable by running this again.

-- Erasure requests: the Article 17(1) ground the erasure was requested under.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'compliance'
                 AND table_name = 'erasure_requests'
                 AND column_name = 'legal_basis')
    THEN
        -- The default backfills every existing row with 0 (not established). It is then dropped so the
        -- column matches what the create script declares: NOT NULL with no default, because the store
        -- always supplies the value on insert.
        ALTER TABLE "compliance"."erasure_requests" ADD COLUMN legal_basis_v2 INT NOT NULL DEFAULT 0;
        ALTER TABLE "compliance"."erasure_requests" ALTER COLUMN legal_basis_v2 DROP DEFAULT;
        ALTER TABLE "compliance"."erasure_requests" DROP COLUMN legal_basis;

        RAISE NOTICE 'compliance.erasure_requests: legal_basis replaced by legal_basis_v2; existing rows read as not established.';
    ELSE
        RAISE NOTICE 'compliance.erasure_requests: already migrated, nothing to do.';
    END IF;
END $$;

-- Erasure certificates: the same ground, projected onto the certificate row.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'compliance'
                 AND table_name = 'erasure_certificates'
                 AND column_name = 'legal_basis')
    THEN
        ALTER TABLE "compliance"."erasure_certificates" ADD COLUMN legal_basis_v2 INT NOT NULL DEFAULT 0;
        ALTER TABLE "compliance"."erasure_certificates" ALTER COLUMN legal_basis_v2 DROP DEFAULT;
        ALTER TABLE "compliance"."erasure_certificates" DROP COLUMN legal_basis;

        RAISE NOTICE 'compliance.erasure_certificates: legal_basis replaced by legal_basis_v2; existing rows read as not established.';
    ELSE
        RAISE NOTICE 'compliance.erasure_certificates: already migrated, nothing to do.';
    END IF;
END $$;

-- Legal holds: the Article 17(3) ground the hold is asserted under.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'compliance'
                 AND table_name = 'legal_holds'
                 AND column_name = 'basis')
    THEN
        ALTER TABLE "compliance"."legal_holds" ADD COLUMN basis_v2 INT NOT NULL DEFAULT 0;
        ALTER TABLE "compliance"."legal_holds" ALTER COLUMN basis_v2 DROP DEFAULT;
        ALTER TABLE "compliance"."legal_holds" DROP COLUMN basis;

        RAISE NOTICE 'compliance.legal_holds: basis replaced by basis_v2; existing holds read as not established.';
    ELSE
        RAISE NOTICE 'compliance.legal_holds: already migrated, nothing to do.';
    END IF;
END $$;

-- WHAT THIS MEANS FOR YOUR EXISTING RECORDS. A pre-upgrade erasure request, certificate or legal hold will
-- report its legal basis as "not established" after this migration. The record itself is intact -- its
-- identifiers, timestamps, counts, signature and every other claim are untouched. Only the legal-basis
-- ordinal is not carried across the renumbering. A signed certificate's own payload is stored separately
-- and in full, so its signature still verifies; this column is a projection of one claim, not the source of
-- it.
--
-- WHAT THIS SCRIPT DOES NOT TOUCH. The consent table carries a different enumeration -- the Article 6
-- lawful basis for processing -- and its column is also named legal_basis. That enumeration is NOT
-- renumbered by this change, so that column is deliberately absent from the blocks above and must NOT be
-- replaced. The blocks name their tables explicitly for this reason.
