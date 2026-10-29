-- One row per member lifetime. Binding creates a NULL position; advances only update the matching lifetime.
-- Greenfield provisioning: existing databases must use this schema. No legacy data migration.
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
