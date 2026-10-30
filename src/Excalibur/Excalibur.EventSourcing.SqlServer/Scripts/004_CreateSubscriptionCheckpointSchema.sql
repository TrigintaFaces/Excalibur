-- One row per member lifetime. Binding creates a NULL position; advances only update the matching lifetime.
-- ddl-migration: replacement-and-replay SubscriptionCheckpoints docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=eng/migrations/alpha19-to-alpha20/Test-Upgrade.ps1 - Preserve legacy state, provision a new lifetime schema and replay fresh destinations; this CREATE does not upgrade an old table.
-- ddl-migration: retained-legacy SubscriptionCheckpointsDefinitions docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=eng/migrations/alpha19-to-alpha20/Test-Upgrade.ps1 - Retain old definitions unchanged for audit and rollback planning; the current store no longer reads this table.
-- Provisioning only: preserve legacy tables and follow the linked upgrade procedure before using this schema.
IF OBJECT_ID(N'dbo.SubscriptionCheckpoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionCheckpoints (
        SubscriptionName NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
        ProjectionKey NVARCHAR(512) COLLATE Latin1_General_BIN2 NOT NULL,
        DefinitionHash VARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CHECK (LEN(DefinitionHash) = 64 AND DefinitionHash NOT LIKE '%[^0-9A-F]%'),
        Destination NVARCHAR(512) COLLATE Latin1_General_BIN2 NOT NULL,
        DestinationScope NVARCHAR(512) COLLATE Latin1_General_BIN2 NULL,
        BindingId VARCHAR(36) COLLATE Latin1_General_BIN2 NOT NULL UNIQUE,
        Position BIGINT NULL CHECK (Position IS NULL OR Position >= 0),
        PRIMARY KEY NONCLUSTERED (SubscriptionName, ProjectionKey)
    );
END;
GO

IF OBJECT_ID(N'dbo.SubscriptionCheckpointsReaders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionCheckpointsReaders (
        SubscriptionName NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
        ReaderName NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
        Revision VARCHAR(36) COLLATE Latin1_General_BIN2 NOT NULL,
        BindingId VARCHAR(36) COLLATE Latin1_General_BIN2 NOT NULL REFERENCES dbo.SubscriptionCheckpoints(BindingId),
        PRIMARY KEY NONCLUSTERED (SubscriptionName, ReaderName)
    );
END;
GO
