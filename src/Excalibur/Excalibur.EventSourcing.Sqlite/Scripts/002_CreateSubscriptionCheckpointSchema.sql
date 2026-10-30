-- One row per member lifetime. Binding creates a NULL position; advances only update the matching lifetime.
-- ddl-migration: replacement-and-replay SubscriptionCheckpoints docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=tests/integration/Excalibur.Integration.Tests/EventSourcing/Sqlite/SqliteSubscriptionCheckpointStoreConformanceShould.cs#ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows - Preserve legacy state, provision a new lifetime schema and replay fresh destinations; this CREATE does not upgrade an old table.
-- ddl-migration: retained-legacy SubscriptionCheckpointsDefinitions docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=tests/integration/Excalibur.Integration.Tests/EventSourcing/Sqlite/SqliteSubscriptionCheckpointStoreConformanceShould.cs#ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows - Retain old definitions unchanged for audit and rollback planning; the current store no longer reads this table.
-- Provisioning only: preserve legacy tables and follow the linked upgrade procedure before using this schema.
CREATE TABLE IF NOT EXISTS [SubscriptionCheckpoints] (
    SubscriptionName TEXT NOT NULL COLLATE BINARY,
    ProjectionKey TEXT NOT NULL COLLATE BINARY,
    DefinitionHash TEXT NOT NULL COLLATE BINARY CHECK (length(DefinitionHash) = 64 AND DefinitionHash NOT GLOB '*[^0-9A-F]*'),
    Destination TEXT NOT NULL COLLATE BINARY,
    DestinationScope TEXT NULL COLLATE BINARY,
    BindingId TEXT NOT NULL COLLATE BINARY UNIQUE,
    Position INTEGER NULL CHECK (Position IS NULL OR Position >= 0),
    PRIMARY KEY (SubscriptionName, ProjectionKey)
);

CREATE TABLE IF NOT EXISTS [SubscriptionCheckpointsReaders] (
    SubscriptionName TEXT NOT NULL COLLATE BINARY,
    ReaderName TEXT NOT NULL COLLATE BINARY,
    Revision TEXT NOT NULL COLLATE BINARY,
    BindingId TEXT NOT NULL COLLATE BINARY REFERENCES [SubscriptionCheckpoints](BindingId),
    PRIMARY KEY (SubscriptionName, ReaderName)
);
