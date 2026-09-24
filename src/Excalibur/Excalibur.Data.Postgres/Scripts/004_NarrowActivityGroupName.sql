-- POSTGRES MIGRATION for Excalibur.Data.Postgres — NARROW activity_group.name TO 128
-- Version: 1.0
--
-- The Postgres twin of Excalibur.Data.SqlServer's 004_NarrowActivityGroupName.sql; read that file's
-- header for the full rationale. This one states only what differs.
--
-- Brings a database provisioned before the narrowing in line with 002_CreateActivityGroupSchema.sql,
-- which now declares name as VARCHAR(128). A database created by the earlier script holds
-- VARCHAR(256) and is NOT repaired by re-running 002: that script is CREATE TABLE IF NOT EXISTS, so
-- it creates nothing and reports success against a table that is simply the wrong shape.
--
-- WHAT DIFFERS FROM THE SQL SERVER SCRIPT
-- ---------------------------------------
-- Postgres has no 900-byte index-key ceiling, so the narrowing here is not forced by the engine. It
-- is applied anyway so both dialects accept exactly the same group names: a name that is valid on
-- one provider and rejected on the other is a portability defect that surfaces only after a
-- consumer migrates. And unlike SQL Server, Postgres alters a type in place without dropping the
-- primary key — it rebuilds the dependent index itself — so there is no constraint dance here.
--
-- THIS SCRIPT REFUSES RATHER THAN TRUNCATES. A group name is an authorization identity; shortening
-- one silently would merge two distinct groups into a single row, which is a privilege change.
--
-- IDEMPOTENT. Re-running against an already-narrowed database does nothing.

DO $$
DECLARE
    current_len integer;
    offenders   text;
BEGIN
    SELECT c.character_maximum_length INTO current_len
    FROM information_schema.columns c
    WHERE c.table_schema = 'authz' AND c.table_name = 'activity_group' AND c.column_name = 'name';

    IF current_len IS NULL THEN
        RAISE NOTICE 'authz.activity_group.name absent — nothing to migrate.';
        RETURN;
    END IF;

    IF current_len <= 128 THEN
        RAISE NOTICE 'authz.activity_group.name is already VARCHAR(128) or narrower — nothing to migrate.';
        RETURN;
    END IF;

    SELECT string_agg(quote_literal(n), ', ')
    INTO offenders
    FROM (SELECT DISTINCT name AS n FROM authz.activity_group WHERE char_length(name) > 128 LIMIT 20) x;

    IF offenders IS NOT NULL THEN
        RAISE EXCEPTION
            'REFUSED: authz.activity_group holds group names longer than 128 characters, so narrowing name would truncate them and merge distinct groups. A group name is an authorization identity. Rename or remove these first (up to 20 shown): %',
            offenders;
    END IF;

    ALTER TABLE "authz"."activity_group" ALTER COLUMN "name" TYPE VARCHAR(128);
    RAISE NOTICE 'authz.activity_group.name narrowed to VARCHAR(128).';
END
$$;
