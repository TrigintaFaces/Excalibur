-- SQL Server Schema for Excalibur.Cdc.SqlServer — CDC STATE STORE
-- Version: 1.0
--
-- Creates the table the SQL Server CDC state store uses to record how far change-data-capture
-- has read each source table. This provider never creates the table at runtime: run this script
-- against the target database before the first checkpoint. Without it, every checkpoint save
-- fails with Invalid object name.
--
-- Note that the Postgres provider DOES create its table on first use. That difference is
-- deliberate on SQL Server, where table creation is usually a privileged, audited operation
-- rather than something an application performs against a production database. The consequence
-- is that this script is required rather than optional.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "Cdc"
--     table  = "CdcProcessingState"
--
-- If you override either, rename the object below to match.
--
--
-- WHY THE NATURAL KEY IS NOT THE CLUSTERED KEY
-- --------------------------------------------
-- A CDC stream is identified by the triple (DatabaseConnectionIdentifier, DatabaseName,
-- TableName) and the store's MERGE matches on exactly those three columns. They cannot form
-- the clustered primary key: at NVARCHAR(256) each they total 1536 bytes, well past SQL
-- Server's 900-byte index key limit. The triple is therefore enforced by a UNIQUE constraint —
-- which carries the same guarantee — and the clustered key is a surrogate identity.

IF SCHEMA_ID(N'Cdc') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [Cdc];');
END
GO

CREATE TABLE [Cdc].[CdcProcessingState]
(
    -- Surrogate clustered key. See the header for why the natural key cannot serve.
    [MessageId]                     BIGINT IDENTITY(1,1)    NOT NULL,

    -- The stream identity. The store's MERGE matches on these three.
    [DatabaseConnectionIdentifier]  NVARCHAR(256)           NOT NULL,
    [DatabaseName]                  NVARCHAR(256)           NOT NULL,
    [TableName]                     NVARCHAR(256)           NOT NULL,

    -- SQL Server log sequence numbers are fixed-width binary(10).
    [LastProcessedLsn]              BINARY(10)              NOT NULL,
    -- Nullable: not every change record carries a sequence value within the LSN.
    [LastProcessedSequenceValue]    BINARY(10)              NULL,
    -- Nullable: a checkpoint can be recorded before a commit time is known.
    [LastCommitTime]                DATETIME2               NULL,

    -- Leader-election fence. NULL when fencing is not configured. The MERGE accepts a
    -- checkpoint only when the incoming token is at least the stored one, so a superseded
    -- leader cannot rewind the progress recorded by its successor.
    [FencingToken]                  BIGINT                  NULL,

    [ProcessedAt]                   DATETIMEOFFSET          NOT NULL DEFAULT SYSDATETIMEOFFSET(),

    CONSTRAINT [PK_CdcProcessingState] PRIMARY KEY CLUSTERED ([MessageId]),
    CONSTRAINT [UQ_CdcProcessingState_Key] UNIQUE
        ([DatabaseConnectionIdentifier], [DatabaseName], [TableName])
);
GO
