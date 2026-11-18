-- PostgreSQL migration for Excalibur.Compliance.Postgres -- legal-basis column replacement
-- Version: 2.0
--
-- WHAT THIS DOES: IT CONVERTS THE STORED ORDINAL. IT DOES NOT DISCARD IT.
--
-- The legal-basis enumerations gained a zero member meaning "no ground was established", and every other
-- member moved up by one. These values are persisted as INT, so every row written before that change holds
-- an ordinal that now names a DIFFERENT legal basis: a stored 1 that meant "legal obligation" would read
-- back as "freedom of expression".
--
-- THE RENUMBERING WAS A PURE SHIFT, which is what makes conversion exact and free. Every member of both
-- affected enumerations moved up by exactly one, and none was renamed, reordered or removed, so old + 1
-- maps every stored value onto the member it was written as. This script adds the new column, backfills
-- it from the old one, and only then drops the old column.
--
-- A VALUE OUTSIDE THE OLD RANGE IS THE ONE CASE THAT CANNOT BE CONVERTED. It named no member of the old
-- enumeration, so it becomes 0 -- "not established" -- and each block REPORTS how many rows that applied
-- to, rather than reporting a clean conversion. If that count is zero, every ground was carried across.
--
-- SUPERSEDED, AND STATED BECAUSE ACTING ON IT COST DATA: an earlier version of this script discarded the
-- old ordinal, landing every row on 0, and justified it by asserting that "this framework can no longer
-- say what ground the row was written under". That was false -- the mapping above was always available.
-- If you have already run that version, the ordinal is gone and cannot be recovered from the database:
-- this script will find no old column and report that there is nothing to do. The tell is a population of
-- pre-upgrade erasure requests, certificates or legal holds whose basis reads "not established" while the
-- records themselves predate your upgrade.
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
DECLARE
    -- Reported so a consumer learns whether anything could NOT be carried across, rather
    -- than being told everything converted.
    converted BIGINT;
    unmapped BIGINT;
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'compliance'
                 AND table_name = 'erasure_requests'
                 AND column_name = 'legal_basis')
    THEN
        -- The default exists only so the column can be added NOT NULL to a populated table; the UPDATE
        -- below immediately overwrites every row with the converted value. It is then dropped so the
        -- column matches what the create script declares: NOT NULL with no default, because the store
        -- always supplies the value on insert.
        ALTER TABLE "compliance"."erasure_requests" ADD COLUMN legal_basis_v2 INT NOT NULL DEFAULT 0;

        -- CONVERT, do not discard. Every member of the old enumeration moved up by exactly one
        -- and none was renamed, reordered or removed, so legal_basis + 1 recovers the stored ground
        -- exactly. A stored value outside the old range named no member of it, and that is the
        -- one case where "not established" is the honest answer rather than a discarded one.
        UPDATE "compliance"."erasure_requests"
           SET legal_basis_v2 = CASE WHEN legal_basis BETWEEN 0 AND 6 THEN legal_basis + 1 ELSE 0 END;

        SELECT count(*) INTO unmapped FROM "compliance"."erasure_requests" WHERE legal_basis_v2 = 0;
        SELECT count(*) INTO converted FROM "compliance"."erasure_requests" WHERE legal_basis_v2 <> 0;

        ALTER TABLE "compliance"."erasure_requests" ALTER COLUMN legal_basis_v2 DROP DEFAULT;
        ALTER TABLE "compliance"."erasure_requests" DROP COLUMN legal_basis;

        RAISE NOTICE 'compliance.erasure_requests: legal_basis converted to legal_basis_v2; % row(s) carried their ground across the renumbering, % row(s) held a value the old enumeration never named and now read as not established.', converted, unmapped;
    ELSE
        RAISE NOTICE 'compliance.erasure_requests: already migrated, nothing to do.';
    END IF;
END $$;

-- Erasure certificates: the same ground, projected onto the certificate row.
DO $$
DECLARE
    -- Reported so a consumer learns whether anything could NOT be carried across, rather
    -- than being told everything converted.
    converted BIGINT;
    unmapped BIGINT;
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'compliance'
                 AND table_name = 'erasure_certificates'
                 AND column_name = 'legal_basis')
    THEN
        ALTER TABLE "compliance"."erasure_certificates" ADD COLUMN legal_basis_v2 INT NOT NULL DEFAULT 0;

        -- CONVERT, do not discard. Every member of the old enumeration moved up by exactly one
        -- and none was renamed, reordered or removed, so legal_basis + 1 recovers the stored ground
        -- exactly. A stored value outside the old range named no member of it, and that is the
        -- one case where "not established" is the honest answer rather than a discarded one.
        UPDATE "compliance"."erasure_certificates"
           SET legal_basis_v2 = CASE WHEN legal_basis BETWEEN 0 AND 6 THEN legal_basis + 1 ELSE 0 END;

        SELECT count(*) INTO unmapped FROM "compliance"."erasure_certificates" WHERE legal_basis_v2 = 0;
        SELECT count(*) INTO converted FROM "compliance"."erasure_certificates" WHERE legal_basis_v2 <> 0;

        ALTER TABLE "compliance"."erasure_certificates" ALTER COLUMN legal_basis_v2 DROP DEFAULT;
        ALTER TABLE "compliance"."erasure_certificates" DROP COLUMN legal_basis;

        RAISE NOTICE 'compliance.erasure_certificates: legal_basis converted to legal_basis_v2; % row(s) carried their ground across the renumbering, % row(s) held a value the old enumeration never named and now read as not established.', converted, unmapped;
    ELSE
        RAISE NOTICE 'compliance.erasure_certificates: already migrated, nothing to do.';
    END IF;
END $$;

-- Legal holds: the Article 17(3) ground the hold is asserted under.
DO $$
DECLARE
    -- Reported so a consumer learns whether anything could NOT be carried across, rather
    -- than being told everything converted.
    converted BIGINT;
    unmapped BIGINT;
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'compliance'
                 AND table_name = 'legal_holds'
                 AND column_name = 'basis')
    THEN
        ALTER TABLE "compliance"."legal_holds" ADD COLUMN basis_v2 INT NOT NULL DEFAULT 0;

        -- CONVERT, do not discard. Every member of the old enumeration moved up by exactly one
        -- and none was renamed, reordered or removed, so basis + 1 recovers the stored ground
        -- exactly. A stored value outside the old range named no member of it, and that is the
        -- one case where "not established" is the honest answer rather than a discarded one.
        UPDATE "compliance"."legal_holds"
           SET basis_v2 = CASE WHEN basis BETWEEN 0 AND 6 THEN basis + 1 ELSE 0 END;

        SELECT count(*) INTO unmapped FROM "compliance"."legal_holds" WHERE basis_v2 = 0;
        SELECT count(*) INTO converted FROM "compliance"."legal_holds" WHERE basis_v2 <> 0;

        ALTER TABLE "compliance"."legal_holds" ALTER COLUMN basis_v2 DROP DEFAULT;
        ALTER TABLE "compliance"."legal_holds" DROP COLUMN basis;

        RAISE NOTICE 'compliance.legal_holds: basis converted to basis_v2; % row(s) carried their ground across the renumbering, % row(s) held a value the old enumeration never named and now read as not established.', converted, unmapped;
    ELSE
        RAISE NOTICE 'compliance.legal_holds: already migrated, nothing to do.';
    END IF;
END $$;

-- WHAT THIS MEANS FOR YOUR EXISTING RECORDS. A pre-upgrade erasure request, certificate or legal hold
-- KEEPS the legal basis it was written under: the ordinal is converted, not dropped, and the record is
-- otherwise untouched -- its identifiers, timestamps, counts and signature are unchanged. A signed
-- certificate's own payload is stored separately and in full, so its signature still verifies; this
-- column is a projection of one claim, not the source of it. Only a value that named no member of the old
-- enumeration lands on "not established", and each block's notice tells you how many rows that was.
--
-- WHAT THIS SCRIPT DOES NOT TOUCH. The consent table carries a different enumeration -- the Article 6
-- lawful basis for processing -- and its column is also named legal_basis. That enumeration is NOT
-- renumbered by this change, so that column is deliberately absent from the blocks above and must NOT be
-- replaced. The blocks name their tables explicitly for this reason.
