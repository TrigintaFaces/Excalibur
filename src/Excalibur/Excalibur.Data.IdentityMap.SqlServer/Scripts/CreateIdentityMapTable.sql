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

-- ddl-migration: provisioning-only IdentityMap - the deliberate pre-RC support policy requires fresh provisioning or reprovisioning.
-- This pre-RC package supports the corrected fresh schema, not an in-place upgrade from an earlier
-- prerelease shape. No upgrade script ships under that policy; a database provisioned before the fix
-- is dropped and recreated. This is a support-policy choice, not a claim that an in-place key change
-- is impossible. This script will not report success on the old shape -- see the check below.

-- SHAPE CHECK
-- -----------
-- Every statement below is guarded on the object being absent, so on an existing database this script
-- would otherwise succeed silently and leave the old, broken key in place. That is the worst outcome:
-- the operator is told the schema is correct, and the first oversized mapping fails at run time with
-- Msg 1946 instead. So an existing table whose primary key is CLUSTERED fails here, loudly.
IF OBJECT_ID(N'dbo.IdentityMap', N'U') IS NOT NULL
   AND EXISTS (SELECT 1
               FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.IdentityMap')
                 AND name = N'PK_IdentityMap'
                 AND type_desc = N'CLUSTERED')
BEGIN
    RAISERROR(
        N'[dbo].[IdentityMap] already exists with PK_IdentityMap declared CLUSTERED. Required: PK_IdentityMap NONCLUSTERED over (ExternalSystem, ExternalId, AggregateType), with a CLUSTERED index CIX_IdentityMap_External over (ExternalSystem, ExternalId). A CLUSTERED key over the full triple is 1280 bytes, past SQL Server''s 900-byte clustered limit, so the table refuses any mapping whose three identifiers together exceed ~450 characters with Msg 1946. The deliberate pre-RC support policy requires fresh provisioning or reprovisioning; no in-place upgrade script ships. Drop [dbo].[IdentityMap] and re-run this script. Mappings are a cache of external identity, rebuildable from the source system; if yours are not, copy the rows out first and insert them back after.',
        16, 1);
END
GO

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
