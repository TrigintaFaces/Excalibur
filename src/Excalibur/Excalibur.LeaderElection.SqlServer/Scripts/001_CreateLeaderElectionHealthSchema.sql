-- SQL Server SCHEMA for Excalibur.LeaderElection.SqlServer — HEALTH-BASED LEADER ELECTION
-- Version: 1.0
--
-- Creates the candidate-health table used by health-based leader election, so a leader is chosen
-- among candidates that are actually healthy rather than merely first to the lock.
--
-- SCOPE — READ THIS BEFORE ASSUMING YOU NEED IT
-- ---------------------------------------------
-- This table is used ONLY by the health-based election path (SqlServerHealthBasedLeaderElection).
-- The package's other leader election path uses sp_getapplock, which lives in the server's lock
-- manager and requires NO table at all. If you are not using the health-based variant, you do not
-- need to run this script and this table will stay empty.
--
-- DERIVED FROM the auto-create path this script mirrors:
--   SqlServerHealthBasedLeaderElection.cs:268-276 (EnsureTableCreatedAsync)
-- The store issues that DDL on first use. This file exists for the deployment that runs without
-- table-creation rights, or that provisions schema centrally ahead of the application. Keep the
-- two in step; a schema written down twice drifts silently.
--
-- OBJECT NAMES: this script targets the DEFAULTS —
--   schema [dbo]                    (SqlServerHealthBasedLeaderElectionOptions.cs:32)
--   table  [LeaderElectionHealth]   (SqlServerHealthBasedLeaderElectionOptions.cs:39)
-- A deployment that configures different names should rename below to match.
--
-- RE-RUNNABLE: every step tests for the state it is about to create. Running it twice, or against
-- a database the store already provisioned, changes nothing.
--
-- WHAT THIS SCRIPT CANNOT DO: the guard is "does this table exist", so against a database
-- provisioned by an older version of this package it is a NO-OP rather than an upgrade. It will
-- not add a column to, or widen a column on, a table that is already there.

-- QUOTED_IDENTIFIER is set explicitly rather than inherited. sqlcmd defaults it OFF, and under
-- OFF a double-quoted string is a literal rather than an identifier — so a script that runs
-- correctly in SSMS (which defaults it ON) can behave differently under the deployment tool that
-- actually runs it. There is no filtered index in this file, which is the case where OFF makes
-- SQL Server refuse the CREATE outright; this is set so the file does not depend on which client
-- invoked it either way.
SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
GO

-- ---------------------------------------------------------------------------------------
-- [dbo].[LeaderElectionHealth] — one row per candidate process.
--
--   CandidateId is the PRIMARY KEY. The store's write path is a single MERGE matching on
--   CandidateId (SqlServerHealthBasedLeaderElection.cs:303-311), so this key is what makes a
--   heartbeat idempotent: a candidate reporting health repeatedly updates its own row instead of
--   accumulating one row per heartbeat.
--
--   INDEX KEY WIDTH: NVARCHAR(256) is 2 bytes per character, so this key is 512 bytes. SQL
--   Server's limit for a clustered/nonclustered index KEY is 900 bytes, and it is stated here
--   because exceeding it is a trap rather than an error: CREATE succeeds with only a WARNING, and
--   the failure arrives later, at INSERT time, on the first row whose key is actually oversized.
--   512 leaves 388 bytes of headroom. Widening this column past NVARCHAR(450) would cross the
--   limit — do not widen it without moving the key.
--
--   The DEFAULTs are meaningful here, unlike on a key column: they define what a row means before
--   the first health report lands. A candidate defaults to HEALTHY (IsHealthy 1, HealthScore 1.0)
--   and NOT leader. Defaulting IsHealthy to 0 would be the more cautious-looking choice and the
--   wrong one — it would make a freshly-inserted candidate ineligible until its second write,
--   which on a cold start is every candidate at once.
--
--   LastUpdated is DATETIMEOFFSET, not DATETIME2. This is a liveness signal compared across
--   processes that need not share a timezone; an offset-less type would make staleness arithmetic
--   depend on where each candidate happens to run. SYSDATETIMEOFFSET() is the matching default.
--
--   MetadataJson is the one nullable column: a candidate reporting no metadata stores NULL rather
--   than an empty document.
--
--   NO tenant column, deliberately. Leader election coordinates PROCESSES, not tenant-owned data
--   — there is no per-tenant leader here, the auto-create path declares no such column, and no
--   query references one. This is why no binary collation is pinned below: that requirement
--   applies to tenant terms, which the framework compares ordinally while a case-insensitive
--   database collation would not. CandidateId is not a tenant term and is matched only against
--   itself.
-- ---------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'dbo')
BEGIN
    EXEC (N'CREATE SCHEMA [dbo]');
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables t
               JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = N'dbo' AND t.name = N'LeaderElectionHealth')
BEGIN
    CREATE TABLE [dbo].[LeaderElectionHealth] (
        CandidateId NVARCHAR(256) NOT NULL PRIMARY KEY,
        IsHealthy BIT NOT NULL DEFAULT 1,
        HealthScore FLOAT NOT NULL DEFAULT 1.0,
        LastUpdated DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        IsLeader BIT NOT NULL DEFAULT 0,
        MetadataJson NVARCHAR(MAX) NULL
    );

    PRINT N'001: [dbo].[LeaderElectionHealth] created.';
END
ELSE
BEGIN
    PRINT N'001: [dbo].[LeaderElectionHealth] already exists — nothing changed.';
END
GO
