-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one with a WHERE
-- clause), and sqlcmd defaults it OFF. [compliance].[LegalHolds] declares one inline, so without
-- this prologue the whole CREATE TABLE fails with Msg 1934 and Msg 1750 and THE TABLE IS NEVER
-- CREATED -- on the surface whose entire purpose is to stop erasure of data a court order says to
-- keep. A consumer who runs this script and does not inspect the exit status gets a compliance
-- schema silently missing its legal-hold table, and learns about it later as Invalid object name.
--
-- Measured on SQL Server 2022 CU26, one variable: without these lines sqlcmd exits 1 and
-- compliance.LegalHolds has ZERO columns; with them it exits 0 and the table has 14.
--
-- ANSI_NULLS is set with it because SQL Server requires both for indexed views and computed-column
-- indexes, and because a script that sets one and not the other invites the same class back.
-- These settings are also required for the PERSISTED computed column and its UNIQUE index on
-- [compliance].[DiscoveredDataLocations]: SQL Server refuses to create, and later refuses to write
-- through, an indexed computed column unless ANSI_NULLS and QUOTED_IDENTIFIER are ON in the session
-- that does it. So the prologue below is load-bearing twice over.
--
--
-- WHY TWO TABLES HAVE A SURROGATE KEY
-- -----------------------------------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At 2 bytes per
-- NVARCHAR character the natural keys here are:
--
--     DataInventoryRegistrations  TableName         NVARCHAR(256)  ->  512 bytes
--                                 FieldName         NVARCHAR(256)  ->  512 bytes
--                                 TenantId          NVARCHAR(255)  ->  510 bytes
--                                                                      ----------
--                                                                      1534 bytes
--
--     DiscoveredDataLocations     DataSubjectIdHash NVARCHAR(128)  ->  256 bytes
--                                 TableName         NVARCHAR(256)  ->  512 bytes
--                                 FieldName         NVARCHAR(256)  ->  512 bytes
--                                 RecordId          NVARCHAR(256)  ->  512 bytes
--                                 TenantId          NVARCHAR(255)  ->  510 bytes
--                                                                      ----------
--                                                                      2302 bytes
--
-- Both exceeded 900, so neither could remain the clustered key. That failure is quiet in the worst
-- way: CREATE TABLE SUCCEEDS with only a warning, and the table then REFUSES oversized inserts at
-- run time with Msg 1946 -- which is not a duplicate key, so no duplicate-key handling absorbs it,
-- and which depends on the DATA rather than the schema, so it passes every smoke test and fails on
-- a consumer's real registration.
--
-- The two tables are repaired differently because only one of them fits the ordinary remedy.
--
--   REGISTRATIONS take a surrogate clustered key with the natural key as a UNIQUE constraint. At
--   1534 bytes it is inside the 1700-byte nonclustered bound, so the guarantee is unchanged and only
--   the physical ordering moves. This is the shape the CDC state store uses, for this same reason.
--
--   DISCOVERED LOCATIONS cannot do that: 2302 exceeds 1700 as well, so no index can carry that key
--   directly. Narrowing was considered and rejected on evidence rather than taste. TableName and
--   FieldName name a table and a column, and a SQL Server identifier maxes at 128 characters, so
--   NVARCHAR(128) is genuinely domain-justified for both -- but that only reaches 1790, still 90
--   bytes over. The remaining 90 would have to come from DataSubjectIdHash, and THAT WIDTH IS NOT
--   OURS TO CHOOSE: the value is produced by a consumer-supplied IDataSubjectHasher, so narrowing it
--   breaks any consumer whose digest is longer than ours. The uniqueness MECHANISM therefore has to
--   change, and the table enforces its natural key through a UNIQUE index on a persisted SHA-256 of
--   a length-prefixed encoding of that key.
--
-- WHAT THAT TRADES, STATED PLAINLY. Uniqueness on that table is now CRYPTOGRAPHIC rather than
-- EXACT. Two distinct natural keys whose SHA-256 collided would be rejected as a duplicate -- the
-- failure presents as a spurious duplicate-key error on insert, NOT as silent data loss or as one
-- row overwriting another. With a length-prefixed encoding the framing cannot manufacture a
-- collision, so the residual risk is SHA-256's own, which is not a practical concern at any table
-- size this will ever reach. It is nonetheless a change in kind, and this is where the next person
-- will look for it. The natural columns are retained as real columns -- the hash is the uniqueness
-- mechanism, not the identity -- so queries, diagnostics and any future repair still have the
-- values.
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

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- SQL Server Schema for Excalibur.Compliance.SqlServer
-- Version: 1.0
--
-- Creates the five tables required by the SQL Server erasure, data-inventory and legal-hold
-- stores. Run this script against the target database before the first request is recorded.
--
-- These stores verify their schema on startup and throw if it is absent, directing you to
-- create it out of band. This script is what that instruction refers to.
--
-- Setting AutoCreateSchema = true on the corresponding options type makes a store create its
-- own tables on first use instead. That is a convenience for development: it requires the
-- application's own credentials to hold DDL rights, which a production deployment usually
-- withholds deliberately, and it puts schema changes outside whatever change control governs
-- this database. These are the erasure and legal-hold surfaces, so that is rarely the right
-- trade. Prefer running this script.
--
-- Schema and table names are configurable. This script uses the defaults, which use the same
-- schema on all three stores:
--
--     SchemaName                    = "compliance"
--     RequestsTableName             = "ErasureRequests"
--     CertificatesTableName         = "ErasureCertificates"
--     RegistrationsTableName        = "DataInventoryRegistrations"
--     DiscoveredLocationsTableName  = "DiscoveredDataLocations"
--     TableName (legal holds)       = "LegalHolds"
--
-- If you override any of those, rename the corresponding object below to match.
--
-- Every statement is guarded, so the script is safe to re-run and safe to apply to a database
-- that already holds some of these tables.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'compliance')
BEGIN
    EXEC('CREATE SCHEMA [compliance]')
END
GO

-- ---------------------------------------------------------------------------
-- Erasure requests
-- ---------------------------------------------------------------------------
-- One row per erasure request, from submission through execution to completion or
-- cancellation. The data subject is stored only as a hash: the request record itself must not
-- become a new copy of the identity it exists to erase.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'ErasureRequests')
BEGIN
    CREATE TABLE [compliance].[ErasureRequests] (
        RequestId             UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DataSubjectIdHash     NVARCHAR(128)    NOT NULL,
        IdType                INT              NOT NULL,
        -- TOTAL, not nullable: an untenanted request holds the reserved sentinel rather than the
        -- absence of a value, so there is exactly one way to say "no tenant". Binary collation so
        -- the database agrees with the framework's ordinal tenant comparison instead of being more
        -- permissive than it, which is a cross-tenant read.
        TenantId              NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_ErasureRequests_TenantId DEFAULT '__untenanted__',
        Scope                 INT              NOT NULL,
        LegalBasis            INT              NOT NULL,
        ExternalReference     NVARCHAR(256)    NULL,
        RequestedBy           NVARCHAR(256)    NOT NULL,
        RequestedAt           DATETIMEOFFSET   NOT NULL,
        ScheduledExecutionAt  DATETIMEOFFSET   NULL,
        ExecutedAt            DATETIMEOFFSET   NULL,
        CompletedAt           DATETIMEOFFSET   NULL,
        CancelledAt           DATETIMEOFFSET   NULL,
        CancellationReason    NVARCHAR(1000)   NULL,
        CancelledBy           NVARCHAR(256)    NULL,
        Status                INT              NOT NULL,
        KeysDeleted           INT              NULL,
        RecordsAffected       INT              NULL,
        CertificateId         UNIQUEIDENTIFIER NULL,
        ErrorMessage          NVARCHAR(2000)   NULL,
        DataCategories        NVARCHAR(MAX)    NULL,
        CreatedAt             DATETIMEOFFSET   NOT NULL,
        UpdatedAt             DATETIMEOFFSET   NOT NULL,
        INDEX IX_ErasureRequests_Status (Status, ScheduledExecutionAt),
        INDEX IX_ErasureRequests_TenantId (TenantId, RequestedAt),
        INDEX IX_ErasureRequests_DataSubject (DataSubjectIdHash)
    )
END
GO

-- ---------------------------------------------------------------------------
-- Erasure certificates
-- ---------------------------------------------------------------------------
-- The signed evidence that an erasure was carried out, retained until RetainUntil. This is the
-- record produced for an auditor, so it outlives the request it certifies.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'ErasureCertificates')
BEGIN
    CREATE TABLE [compliance].[ErasureCertificates] (
        CertificateId         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        RequestId             UNIQUEIDENTIFIER NOT NULL,
        DataSubjectReference  NVARCHAR(256)    NOT NULL,
        RequestReceivedAt     DATETIMEOFFSET   NOT NULL,
        CompletedAt           DATETIMEOFFSET   NOT NULL,
        Method                INT              NOT NULL,
        Summary               NVARCHAR(MAX)    NOT NULL,
        Verification          NVARCHAR(MAX)    NOT NULL,
        LegalBasis            INT              NOT NULL,
        Signature             NVARCHAR(512)    NOT NULL,
        RetainUntil           DATETIMEOFFSET   NOT NULL,
        CreatedAt             DATETIMEOFFSET   NOT NULL,
        INDEX IX_ErasureCertificates_RequestId (RequestId),
        INDEX IX_ErasureCertificates_RetainUntil (RetainUntil)
    )
END
GO

-- ---------------------------------------------------------------------------
-- Data inventory registrations
-- ---------------------------------------------------------------------------
-- Declares which of YOUR tables and fields hold personal data, so the erasure path knows where
-- to look. A field that is not registered here is a field erasure will not reach.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'DataInventoryRegistrations')
BEGIN
    CREATE TABLE [compliance].[DataInventoryRegistrations] (
        -- Surrogate clustered key. The natural key (TableName, FieldName, TenantId) is 1534 bytes,
        -- past SQL Server's 900-byte CLUSTERED limit, so it cannot be the clustered key. It remains
        -- enforced, as a UNIQUE constraint, which is bounded at 1700 instead. See the header.
        RegistrationId       BIGINT IDENTITY(1,1) NOT NULL,
        TableName            NVARCHAR(256)  NOT NULL,
        FieldName            NVARCHAR(256)  NOT NULL,
        DataCategory         NVARCHAR(256)  NOT NULL,
        DataSubjectIdColumn  NVARCHAR(256)  NOT NULL,
        IdType               INT            NOT NULL,
        KeyIdColumn          NVARCHAR(256)  NOT NULL,
        -- The NAME of a tenant column in your own table. Nullable because your table may
        -- genuinely have none. This is not a tenant identity -- see TenantId below.
        TenantIdColumn       NVARCHAR(256)  NULL,
        -- The tenant this registration BELONGS to. NOT NULL with an explicit sentinel default:
        -- a nullable tenant makes "global" and "forgot to set it" indistinguishable, and the
        -- store cannot tell which one it is holding.
        --
        -- The binary collation is load-bearing. Under the server's usual case-insensitive
        -- default, tenant 'Acme' matches 'acme', so the tenant predicate fails OPEN.
        TenantId             NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_DataInventoryRegistrations_TenantId DEFAULT '__untenanted__',
        Description          NVARCHAR(1000) NULL,
        CreatedAt            DATETIMEOFFSET NOT NULL,
        UpdatedAt            DATETIMEOFFSET NOT NULL,
        -- TenantId is part of the natural KEY, not merely a column: without it two tenants
        -- registering the same table and field are ONE row, and the second write silently destroys
        -- the first -- taking with it the erasure path's only record that the field exists.
        --
        -- The guarantee is unchanged by moving it off the clustered key. A UNIQUE constraint
        -- enforces exactly the same uniqueness; only the physical ordering differs.
        CONSTRAINT PK_DataInventoryRegistrations PRIMARY KEY CLUSTERED (RegistrationId),
        CONSTRAINT UQ_DataInventoryRegistrations_Key
            UNIQUE (TableName, FieldName, TenantId),
        INDEX IX_DataInventoryRegistrations_DataCategory (DataCategory)
    )
END
GO

-- ---------------------------------------------------------------------------
-- Discovered data locations
-- ---------------------------------------------------------------------------
-- Where a specific data subject's data was actually found, resolved from the registrations
-- above. This is the working set an erasure run acts on.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'DiscoveredDataLocations')
BEGIN
    CREATE TABLE [compliance].[DiscoveredDataLocations] (
        -- Surrogate clustered key. This table's natural key is 2302 bytes -- past the 900-byte
        -- CLUSTERED limit AND past the 1700-byte NONCLUSTERED one -- so unlike the registrations
        -- table above it cannot be enforced directly by any index. See the header for why the
        -- columns cannot simply be narrowed, and what the hash below trades for that.
        LocationId         BIGINT IDENTITY(1,1) NOT NULL,
        DataSubjectIdHash  NVARCHAR(128)  NOT NULL,
        TableName          NVARCHAR(256)  NOT NULL,
        FieldName          NVARCHAR(256)  NOT NULL,
        RecordId           NVARCHAR(256)  NOT NULL,
        DataCategory       NVARCHAR(256)  NOT NULL,
        KeyId              NVARCHAR(256)  NOT NULL,
        IsAutoDiscovered   BIT            NOT NULL DEFAULT 1,
        -- The tenant this discovered location belongs to, on the same terms as the
        -- registrations table above, including the binary collation.
        TenantId           NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_DiscoveredDataLocations_TenantId DEFAULT '__untenanted__',
        CreatedAt          DATETIMEOFFSET NOT NULL,
        UpdatedAt          DATETIMEOFFSET NOT NULL,
        -- The natural key, hashed, because it cannot be indexed directly. Every component is
        -- LENGTH-PREFIXED rather than delimiter-joined: a plain join is ambiguous whenever a value
        -- may contain the delimiter, so ('ab','c') and ('a','bc') would collapse to one hash and
        -- reintroduce the very collision this key exists to prevent -- for any consumer whose table,
        -- field or record identifiers contain the separator. Length prefixes make the encoding
        -- injective for arbitrary inputs, so the only remaining collision risk is SHA-256 itself
        -- rather than the framing. Same reasoning, and same shape, as the composite keys elsewhere
        -- in this subsystem.
        --
        -- DATALENGTH, not LEN: LEN ignores trailing spaces, so 'a' and 'a ' would report the same
        -- length and the prefix would stop being injective for exactly the values it must separate.
        -- DATALENGTH counts bytes and never trims.
        --
        -- PERSISTED so the value is computed once and can carry an index. Every input is NOT NULL,
        -- so the hash is never NULL; were one to become nullable the hash would go NULL and the
        -- UNIQUE constraint would admit only one such row, which is a change to make deliberately.
        NaturalKeyHash AS CAST(HASHBYTES('SHA2_256',
                CAST(DATALENGTH(DataSubjectIdHash) AS BINARY(4)) + CAST(DataSubjectIdHash AS VARBINARY(4000))
              + CAST(DATALENGTH(TableName)         AS BINARY(4)) + CAST(TableName         AS VARBINARY(4000))
              + CAST(DATALENGTH(FieldName)         AS BINARY(4)) + CAST(FieldName         AS VARBINARY(4000))
              + CAST(DATALENGTH(RecordId)          AS BINARY(4)) + CAST(RecordId          AS VARBINARY(4000))
              + CAST(DATALENGTH(TenantId)          AS BINARY(4)) + CAST(TenantId          AS VARBINARY(4000))
            ) AS BINARY(32)) PERSISTED,
        -- TenantId is in the natural KEY: two tenants discovering the same record for the same data
        -- subject are two distinct findings, not one overwriting the other. It is a hash component
        -- for that reason, not for isolation.
        CONSTRAINT PK_DiscoveredDataLocations PRIMARY KEY CLUSTERED (LocationId),
        CONSTRAINT UQ_DiscoveredDataLocations_Key UNIQUE (NaturalKeyHash),
        INDEX IX_DiscoveredDataLocations_DataSubject (DataSubjectIdHash),
        INDEX IX_DiscoveredDataLocations_Table (TableName, FieldName)
    )
END
GO

-- ---------------------------------------------------------------------------
-- Legal holds
-- ---------------------------------------------------------------------------
-- A hold suspends erasure for the subject it names. The erasure path consults this table
-- before acting, so it must exist wherever erasure runs.
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'LegalHolds')
BEGIN
    CREATE TABLE [compliance].[LegalHolds] (
        HoldId             UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DataSubjectIdHash  NVARCHAR(128)    NULL,
        IdType             INT              NULL,
        -- TOTAL, not nullable. An untenanted hold is a GLOBAL hold — it blocks erasure for every
        -- tenant — and it now says so with the reserved sentinel rather than with a NULL. The read
        -- predicate matches that sentinel explicitly; a scoped tenant must still SEE a global hold,
        -- because losing one does not fail safe, it erases data a court order says to keep.
        TenantId           NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_LegalHolds_TenantId DEFAULT '__untenanted__',
        Basis              INT              NOT NULL,
        CaseReference      NVARCHAR(256)    NOT NULL,
        Description        NVARCHAR(2000)   NOT NULL,
        IsActive           BIT              NOT NULL DEFAULT 1,
        ExpiresAt          DATETIMEOFFSET   NULL,
        CreatedBy          NVARCHAR(256)    NOT NULL,
        CreatedAt          DATETIMEOFFSET   NOT NULL,
        ReleasedBy         NVARCHAR(256)    NULL,
        ReleasedAt         DATETIMEOFFSET   NULL,
        ReleaseReason      NVARCHAR(1000)   NULL,
        INDEX IX_LegalHolds_DataSubject (DataSubjectIdHash, IsActive),
        INDEX IX_LegalHolds_TenantId (TenantId, IsActive),
        INDEX IX_LegalHolds_ExpiresAt (IsActive, ExpiresAt) WHERE IsActive = 1 AND ExpiresAt IS NOT NULL
    )
END
GO
