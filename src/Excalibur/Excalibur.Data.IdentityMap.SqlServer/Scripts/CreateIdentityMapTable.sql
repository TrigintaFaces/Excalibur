-- Identity Map table for Excalibur.Data.IdentityMap.SqlServer
-- Maps external system identifiers to internal aggregate IDs.
--
-- Schema and table name are configurable via SqlServerIdentityMapOptions.
-- This script uses default values: [dbo].[IdentityMap].

IF OBJECT_ID(N'dbo.IdentityMap', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IdentityMap] (
        ExternalSystem  NVARCHAR(128)    NOT NULL,
        ExternalId      NVARCHAR(256)    NOT NULL,
        AggregateType   NVARCHAR(256)    NOT NULL,
        AggregateId     NVARCHAR(256)    NOT NULL,
        CreatedAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_IdentityMap_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_IdentityMap_UpdatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_IdentityMap PRIMARY KEY CLUSTERED (ExternalSystem, ExternalId, AggregateType)
    );

    CREATE NONCLUSTERED INDEX IX_IdentityMap_AggregateId
        ON [dbo].[IdentityMap] (AggregateType, AggregateId);
END;
GO
