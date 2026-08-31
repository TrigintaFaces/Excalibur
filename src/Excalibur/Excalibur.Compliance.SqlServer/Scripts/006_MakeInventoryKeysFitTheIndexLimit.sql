-- SQL Server MIGRATION for Excalibur.Compliance.SqlServer — DATA INVENTORY KEY WIDTHS
-- Version: 1.0
--
-- Repairs two primary keys that were created over SQL Server's index key limit. This is a REPAIR of
-- an existing fault, not the addition of a constraint: both tables have been refusing large rows
-- since the day they were created, and on a database whose values never got large enough to trip it,
-- nothing has gone wrong yet and nothing will announce that it is about to.
--
--
-- WHAT WAS WRONG
-- --------------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At 2 bytes per
-- NVARCHAR character the shipped keys were:
--
--     PK_DataInventoryRegistrations  TableName         NVARCHAR(256)  ->  512 bytes
--                                    FieldName         NVARCHAR(256)  ->  512 bytes
--                                    TenantId          NVARCHAR(255)  ->  510 bytes
--                                                                         ----------
--                                                                         1534 bytes   (of 900)
--
--     PK_DiscoveredDataLocations     DataSubjectIdHash NVARCHAR(128)  ->  256 bytes
--                                    TableName         NVARCHAR(256)  ->  512 bytes
--                                    FieldName         NVARCHAR(256)  ->  512 bytes
--                                    RecordId          NVARCHAR(256)  ->  512 bytes
--                                    TenantId          NVARCHAR(255)  ->  510 bytes
--                                                                         ----------
--                                                                         2302 bytes   (of 900)
--
-- CREATE TABLE succeeded on both, emitting only "Warning! The maximum key length for a clustered
-- index is 900 bytes." The table then refuses any insert whose key values exceed 900 with Msg 1946.
-- That is not a duplicate key, so no duplicate-key handling absorbs it, and it depends on the DATA
-- rather than the schema — so it survives every smoke test and fails on a real registration.
--
--
-- AFTER
-- -----
-- Each table takes a surrogate BIGINT IDENTITY as its clustered key. The natural keys are unchanged
-- as data and remain enforced; only the mechanism enforcing them moves.
--
--   REGISTRATIONS       natural key becomes a UNIQUE constraint. 1534 bytes is inside the 1700-byte
--                       nonclustered bound, so this is the ordinary remedy and the guarantee is
--                       identical — same columns, same uniqueness, different physical ordering.
--
--   DISCOVERED LOCATIONS cannot use that remedy: 2302 exceeds 1700 as well, so NO index can carry
--                       that key directly. Narrowing was considered and rejected on evidence.
--                       TableName and FieldName name a table and a column and a SQL Server
--                       identifier maxes at 128 characters, so NVARCHAR(128) is domain-justified for
--                       both — but that only reaches 1790, still 90 bytes over. The remaining 90
--                       would have to come from DataSubjectIdHash, and that width is NOT OURS TO
--                       CHOOSE: the value comes from a consumer-supplied IDataSubjectHasher, so
--                       narrowing it breaks any consumer whose digest is longer than ours.
--                       The table therefore enforces its natural key through a UNIQUE index on a
--                       PERSISTED SHA-256 of a LENGTH-PREFIXED encoding of that key.
--
--
-- WHAT THIS TRADES, STATED RATHER THAN IMPLIED
-- --------------------------------------------
-- Uniqueness on DiscoveredDataLocations becomes CRYPTOGRAPHIC rather than EXACT. Two distinct
-- natural keys whose SHA-256 collided would be rejected as a duplicate. The failure presents as a
-- SPURIOUS DUPLICATE-KEY ERROR on insert — not as silent data loss, and not as one row overwriting
-- another. Because every component is length-prefixed the framing cannot manufacture a collision
-- (a delimiter-joined encoding could: ('ab','c') and ('a','bc') collapse to one string for any
-- delimiter a value may contain), so the residual risk is SHA-256's own and is not a practical
-- concern at any size this table will reach. It is a change in kind nonetheless, which is why it is
-- written here rather than left for someone to infer from the DDL.
--
-- DATALENGTH is used rather than LEN for the prefixes: LEN ignores trailing spaces, so 'a' and 'a '
-- would report the same length and the prefix would stop being injective for exactly the values it
-- exists to separate.
--
-- The natural columns are RETAINED as real columns. The hash is the uniqueness mechanism, not the
-- identity — queries, diagnostics and any future repair all need the actual values.
--
-- A CONSEQUENCE THAT REACHES EVERY WRITER, NOT JUST THIS SCRIPT
-- -------------------------------------------------------------
-- An indexed computed column constrains the SESSION SETTINGS OF EVERY CONNECTION THAT WRITES to the
-- table, not only the one that created it. SQL Server refuses an INSERT or UPDATE from a session
-- whose QUOTED_IDENTIFIER is OFF, with Msg 1934 -- the same number the missing prologue produced,
-- arriving at a completely different moment.
--
-- MEASURED, on the migrated table: an INSERT through sqlcmd, which defaults QUOTED_IDENTIFIER OFF,
-- fails with 'INSERT failed because the following SET options have incorrect settings:
-- QUOTED_IDENTIFIER'. The same INSERT preceded by SET QUOTED_IDENTIFIER ON succeeds.
--
-- The application path is unaffected: SqlClient turns QUOTED_IDENTIFIER ON when it connects, which
-- is why the store's own writes work without doing anything. What this does affect is anything that
-- writes to this table OUTSIDE the application -- ad-hoc sqlcmd repair, a bulk import, an ETL job,
-- a DBA fixing a row by hand. Those must set QUOTED_IDENTIFIER ON or they will be refused, and the
-- error names the setting rather than the cause, so it is worth knowing before it happens at 3am.
--
-- This is a real cost of enforcing uniqueness through a computed column and is recorded here beside
-- the cryptographic-uniqueness trade rather than left to be discovered.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run with the compliance stores stopped, after upgrading the package. Adding an IDENTITY column and
-- rebuilding a clustered index are size-of-data operations holding a schema-modification lock. Take
-- a backup you have restored at least once.
--
-- No backfill is required: the surrogate keys are generated by IDENTITY and the hash by a computed
-- column, so every existing row acquires both without a data rewrite of its own columns.
--
-- SCOPE: this script targets the DEFAULT object names created by 001. A deployment that renamed
-- them must rename the objects below to match.
--
-- Every statement is guarded against the state it is about to create, so the script is safe to
-- re-run and is a no-op on a database already provisioned from the current 001.

-- Required to CREATE and to WRITE THROUGH an indexed computed column. sqlcmd defaults
-- QUOTED_IDENTIFIER OFF, under which step 4 below fails with Msg 1934 and the constraint is simply
-- absent — the same trap that hid a missing LegalHolds table in the base schema.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- Explicit transaction wrapper -- see Excalibur.EventSourcing.SqlServer's
-- 006_ConvergeUntenantedToDefaultTenant.sql header for why: without it, the
-- DiscoveredDataLocations collision guard's RAISERROR does not roll back the Registrations
-- surrogate-key conversion an earlier step already made (measured live against this package's
-- Postgres twin, 004_ConvergeDefaultToUntenanted.sql; the same defect, same mechanism).
--
-- The transaction is one half. The other is the one-line guard at the top of every batch below,
-- and neither replaces the other:
--
--   XACT_ABORT ON rolls the transaction back the moment a guard's THROW fires. But GO is a CLIENT
--   batch separator, not a statement -- whatever applies this script sends each batch to the
--   server as a SEPARATE unit, and once the transaction is gone the batches AFTER the refusal
--   would run unprotected in autocommit and do the work anyway. The rollback makes what already
--   ran reversible; something else has to stop what has not run yet.
--
--   IF @@TRANCOUNT = 0 SET NOEXEC ON; is that something. A rolled-back transaction leaves
--   @@TRANCOUNT at zero, so the first batch after a refusal turns execution off for the rest of
--   the session and every later batch is compiled but never run. SET NOEXEC OFF at the very end
--   restores the session on the path where nothing refused.
--
--
-- BOTH OF THOSE ASSUME THE WHOLE SCRIPT IS APPLIED ON ONE CONNECTION. A transaction belongs to a
-- session, so a client that reconnects between batches loses it at the first GO -- and the guard
-- would then quietly switch the rest of the script off, which is the "completed having done
-- nothing" outcome this migration exists to refuse. The batch immediately below the transaction
-- therefore checks for that case explicitly and REFUSES, naming it. It is the one batch that can:
-- nothing has run yet that could have refused, so @@TRANCOUNT = 0 there means the session was lost
-- and nothing else. sqlcmd keeps one connection by default; a migration runner may need telling.
-- That guard is deliberately plain T-SQL rather than sqlcmd's :on error exit. The directive does
-- the same job, but it is a CLIENT command rather than a statement, so any tool that is not
-- sqlcmd sends it to the server and the whole script dies on its first line with
-- "Incorrect syntax near ':'" -- having done nothing at all. This script is meant to be applied
-- by whatever your deployment already uses, not only by sqlcmd.
--
-- ONE THING THE GUARD CANNOT DO IS SET THE PROCESS EXIT CODE. On a refusal sqlcmd still exits 0
-- unless you pass -b. If a pipeline branches on this script's exit code, run it as
--     sqlcmd -b -i <this script>
-- or the pipeline will read a refused, no-op migration as a success.

SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

-- The transaction opened in the batch above must still be here. If it is not, the client did not
-- keep one session across this script's batches -- see the header. This is the only batch that can
-- tell that apart from a deliberate refusal, because nothing has run yet that could have refused.
IF @@TRANCOUNT = 0
BEGIN
    THROW 51006, N'This migration opens a transaction in its first batch and commits it in its last, so the whole script must be applied on a SINGLE connection. @@TRANCOUNT is 0 here, which means the client reconnected after the opening batch and the transaction is already gone: every remaining batch would run unprotected, and a refusal partway through would leave the database half-migrated with no way back. Refusing rather than converting anything. Apply the whole script on one connection (sqlcmd does this by default), then re-run.', 1;
END

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- STEP 1 — DataInventoryRegistrations: surrogate clustered key
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = N'compliance' AND t.name = N'DataInventoryRegistrations')
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]')
                     AND name = N'RegistrationId')
BEGIN
    ALTER TABLE [compliance].[DataInventoryRegistrations]
        ADD RegistrationId BIGINT IDENTITY(1,1) NOT NULL;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- Drop the over-limit key only when it is still the wide one. Keyed on the column count of the
-- constraint rather than on its name, so a database already migrated is left alone.
IF EXISTS (SELECT 1 FROM sys.key_constraints kc
           JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
           WHERE kc.name = N'PK_DataInventoryRegistrations'
           GROUP BY kc.name HAVING COUNT(*) > 1)
BEGIN
    ALTER TABLE [compliance].[DataInventoryRegistrations]
        DROP CONSTRAINT PK_DataInventoryRegistrations;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_DataInventoryRegistrations')
BEGIN
    ALTER TABLE [compliance].[DataInventoryRegistrations]
        ADD CONSTRAINT PK_DataInventoryRegistrations PRIMARY KEY CLUSTERED (RegistrationId);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- The natural key, preserved as a UNIQUE constraint. 1534 bytes, inside the 1700-byte nonclustered
-- bound. Creating this cannot fail on existing data: the primary key it replaces enforced exactly
-- these three columns, so they are already distinct.
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_DataInventoryRegistrations_Key')
BEGIN
    ALTER TABLE [compliance].[DataInventoryRegistrations]
        ADD CONSTRAINT UQ_DataInventoryRegistrations_Key
            UNIQUE (TableName, FieldName, TenantId);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- STEP 2 — DiscoveredDataLocations: surrogate clustered key
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = N'compliance' AND t.name = N'DiscoveredDataLocations')
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
                     AND name = N'LocationId')
BEGIN
    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ADD LocationId BIGINT IDENTITY(1,1) NOT NULL;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- STEP 3 — the length-prefixed hash of the natural key
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = N'compliance' AND t.name = N'DiscoveredDataLocations')
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
                     AND name = N'NaturalKeyHash')
BEGIN
    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ADD NaturalKeyHash AS CAST(HASHBYTES('SHA2_256',
                CAST(DATALENGTH(DataSubjectIdHash) AS BINARY(4)) + CAST(DataSubjectIdHash AS VARBINARY(4000))
              + CAST(DATALENGTH(TableName)         AS BINARY(4)) + CAST(TableName         AS VARBINARY(4000))
              + CAST(DATALENGTH(FieldName)         AS BINARY(4)) + CAST(FieldName         AS VARBINARY(4000))
              + CAST(DATALENGTH(RecordId)          AS BINARY(4)) + CAST(RecordId          AS VARBINARY(4000))
              + CAST(DATALENGTH(TenantId)          AS BINARY(4)) + CAST(TenantId          AS VARBINARY(4000))
            ) AS BINARY(32)) PERSISTED;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- STEP 4 — COLLISION PRE-FLIGHT, then the unique constraint
--
-- The unique index would fail on its own if two rows hashed alike, but it would fail without saying
-- WHICH rows, and on a compliance table that is the difference between a fixable morning and a
-- forensic one. This check refuses loudly and NAMES the colliding natural keys instead. It does not
-- pick a winner: a genuine collision here is either two identical natural keys (impossible while the
-- old primary key stood) or a SHA-256 collision, and neither is something a script may resolve by
-- deleting somebody's compliance record.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
             AND name = N'NaturalKeyHash')
   AND NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_DiscoveredDataLocations_Key')
BEGIN
    IF EXISTS (SELECT 1 FROM [compliance].[DiscoveredDataLocations]
               GROUP BY NaturalKeyHash HAVING COUNT(*) > 1)
    BEGIN
        DECLARE @collisions NVARCHAR(MAX) = (
            SELECT STRING_AGG(CAST(line AS NVARCHAR(MAX)), CHAR(10))
            FROM (
                SELECT TOP 20
                       CONVERT(VARCHAR(64), NaturalKeyHash, 2) + N'  x' + CAST(COUNT(*) AS NVARCHAR(10))
                       -- COLLATE DATABASE_DEFAULT on every component: TenantId is pinned to
                       -- Latin1_General_BIN2 while its neighbours use the database default, and
                       -- concatenating across two collations raises Msg 4191 and takes down the
                       -- guard rather than the thing it guards against.
                       + N'  e.g. subject=' + MIN(DataSubjectIdHash) COLLATE DATABASE_DEFAULT
                       + N' table='  + MIN(TableName)  COLLATE DATABASE_DEFAULT
                       + N' field='  + MIN(FieldName)  COLLATE DATABASE_DEFAULT
                       + N' record=' + MIN(RecordId)   COLLATE DATABASE_DEFAULT
                       + N' tenant=' + MIN(TenantId)   COLLATE DATABASE_DEFAULT AS line
                FROM [compliance].[DiscoveredDataLocations]
                GROUP BY NaturalKeyHash HAVING COUNT(*) > 1
            ) AS c);

        -- Built into one variable and raised as-is. RAISERROR accepts only variables and constants
        -- as substitution arguments, not function calls, so a CHAR(10) passed inline is a syntax
        -- error that would take the whole batch down before this guard could ever report anything.
        DECLARE @msg NVARCHAR(MAX) =
            N'REFUSING TO MIGRATE: [compliance].[DiscoveredDataLocations] holds rows whose natural '
          + N'keys hash alike, so a UNIQUE constraint on the hash cannot be created without '
          + N'discarding one of them. This script will not choose which compliance record to '
          + N'destroy. Up to 20 shown:' + NCHAR(13) + NCHAR(10) + ISNULL(@collisions, N'(none listed)');

        RAISERROR(@msg, 16, 1);
    END
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF EXISTS (SELECT 1 FROM sys.key_constraints kc
           JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
           WHERE kc.name = N'PK_DiscoveredDataLocations'
           GROUP BY kc.name HAVING COUNT(*) > 1)
BEGIN
    ALTER TABLE [compliance].[DiscoveredDataLocations]
        DROP CONSTRAINT PK_DiscoveredDataLocations;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_DiscoveredDataLocations')
BEGIN
    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ADD CONSTRAINT PK_DiscoveredDataLocations PRIMARY KEY CLUSTERED (LocationId);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_DiscoveredDataLocations_Key')
BEGIN
    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ADD CONSTRAINT UQ_DiscoveredDataLocations_Key UNIQUE (NaturalKeyHash);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
