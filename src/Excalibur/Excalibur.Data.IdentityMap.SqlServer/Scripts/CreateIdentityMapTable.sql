-- Identity Map table for Excalibur.Data.IdentityMap.SqlServer
-- Maps external system identifiers to internal aggregate IDs.
--
-- Schema and table name are configurable via SqlServerIdentityMapOptions.
-- This script uses default values: [dbo].[IdentityMap].
--
-- KEY WIDTH
-- ---------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At 2 bytes
-- per NVARCHAR character the natural key here is:
--
--     ExternalSystem  NVARCHAR(128)  ->   256 bytes
--     ExternalId      NVARCHAR(256)  ->   512 bytes
--     AggregateType   NVARCHAR(256)  ->   512 bytes
--                                      -----------
--                                        1280 bytes
--
-- A plain PRIMARY KEY defaults to CLUSTERED, and that fails in the worst possible way: CREATE TABLE
-- SUCCEEDS with only warning Msg 1946, and the table then REFUSES oversized rows at run time with
-- Msg 1946 again. A deployment whose external system, external id and aggregate type together run
-- past ~450 characters cannot record a mapping, and finds out on the first such write rather than at
-- deployment time. So the key is declared NONCLUSTERED, where 1700 bytes gives it room, and the
-- table is clustered on the widest prefix that fits -- (ExternalSystem, ExternalId) at 768 bytes.
-- The uniqueness guarantee is identical either way.
--
-- The clustered key is deliberately NOT unique: one external identity may map to more than one
-- aggregate type, which is the whole point of carrying AggregateType in the primary key.

IF OBJECT_ID(N'dbo.IdentityMap', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IdentityMap] (
        ExternalSystem  NVARCHAR(128)    NOT NULL,
        ExternalId      NVARCHAR(256)    NOT NULL,
        AggregateType   NVARCHAR(256)    NOT NULL,
        AggregateId     NVARCHAR(256)    NOT NULL,
        CreatedAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_IdentityMap_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_IdentityMap_UpdatedAt DEFAULT SYSUTCDATETIME(),

        -- NONCLUSTERED is load-bearing, not a tuning choice. See KEY WIDTH in the header: the
        -- triple is 1280 bytes, past SQL Server's 900-byte CLUSTERED cap and inside the 1700-byte
        -- NONCLUSTERED one. Declaring it CLUSTERED creates the table successfully and then rejects
        -- any row whose key exceeds 900 bytes.
        CONSTRAINT PK_IdentityMap PRIMARY KEY NONCLUSTERED (ExternalSystem, ExternalId, AggregateType)
    );

    -- The table needs a clustered key and the primary key cannot be it. This prefix is 768 bytes and
    -- is also the lookup order the resolver uses, so it earns its place twice.
    CREATE CLUSTERED INDEX CIX_IdentityMap_External
        ON [dbo].[IdentityMap] (ExternalSystem, ExternalId);

    CREATE NONCLUSTERED INDEX IX_IdentityMap_AggregateId
        ON [dbo].[IdentityMap] (AggregateType, AggregateId);
END;
GO
