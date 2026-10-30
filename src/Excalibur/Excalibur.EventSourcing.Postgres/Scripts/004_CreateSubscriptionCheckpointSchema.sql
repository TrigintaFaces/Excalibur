-- One row per member lifetime. Binding creates a NULL position; advances only update the matching lifetime.
-- ddl-migration: replacement-and-replay subscription_checkpoints docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=tests/integration/Excalibur.Integration.Tests/EventSourcing/Postgres/PostgresSubscriptionCheckpointStoreConformanceShould.cs#ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows - Preserve legacy state, provision a new lifetime schema and replay fresh destinations; this CREATE does not upgrade an old table.
-- ddl-migration: retained-legacy subscription_checkpoints_definitions docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=tests/integration/Excalibur.Integration.Tests/EventSourcing/Postgres/PostgresSubscriptionCheckpointStoreConformanceShould.cs#ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows - Retain old definitions unchanged for audit and rollback planning; the current store no longer reads this table.
-- Provisioning only: preserve legacy tables and follow the linked upgrade procedure before using this schema.
CREATE TABLE IF NOT EXISTS public.subscription_checkpoints (
    subscription_name VARCHAR(255) COLLATE "C" NOT NULL,
    projection_key VARCHAR(512) COLLATE "C" NOT NULL,
    definition_hash VARCHAR(64) COLLATE "C" NOT NULL CHECK (definition_hash ~ '^[0-9A-F]{64}$'),
    destination VARCHAR(512) COLLATE "C" NOT NULL,
    destination_scope VARCHAR(512) COLLATE "C" NULL,
    binding_id VARCHAR(36) COLLATE "C" NOT NULL UNIQUE,
    position BIGINT NULL CHECK (position IS NULL OR position >= 0),
    PRIMARY KEY (subscription_name, projection_key)
);

CREATE TABLE IF NOT EXISTS public.subscription_checkpoints_readers (
    subscription_name VARCHAR(255) COLLATE "C" NOT NULL,
    reader_name VARCHAR(255) COLLATE "C" NOT NULL,
    revision VARCHAR(36) COLLATE "C" NOT NULL,
    binding_id VARCHAR(36) COLLATE "C" NOT NULL REFERENCES public.subscription_checkpoints(binding_id),
    PRIMARY KEY (subscription_name, reader_name)
);
