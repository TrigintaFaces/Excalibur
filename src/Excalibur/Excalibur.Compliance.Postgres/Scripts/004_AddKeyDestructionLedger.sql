-- ddl-migration: provisioning-only erasure_destroyed_keys - Earlier alpha ledger identities require a new schema; incompatible tables are rejected without deleting destruction evidence.
-- ddl-migration: provisioning-only erasure_destruction_intents - Existing staging tables are validated before creation; incompatible shapes are rejected without changing rows or columns.
-- Provision absent ledger tables; validate existing tables without altering records.
-- Earlier alpha schemas are unsupported: provision a new compliance schema instead.
-- All checks precede creation; a transaction-owned lock serializes this script.
BEGIN;
SELECT pg_advisory_xact_lock(728164921047);
DO $$
BEGIN
    IF to_regclass('compliance.erasure_destroyed_keys') IS NOT NULL THEN
        IF (SELECT array_agg(a.attname::text ORDER BY k.ordinality) FROM pg_constraint c CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum, ordinality) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.attnum WHERE c.conrelid=to_regclass('compliance.erasure_destroyed_keys') AND c.contype='p') IS DISTINCT FROM ARRAY['key_handle','key_generation']::text[] THEN
            RAISE EXCEPTION 'Incompatible erasure_destroyed_keys primary key; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destroyed_keys' AND column_name='key_generation' AND data_type='text' AND is_nullable='NO' AND collation_schema='pg_catalog' AND collation_name='C') THEN
            RAISE EXCEPTION 'Incompatible erasure_destroyed_keys.key_generation; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destroyed_keys' AND column_name='request_id' AND data_type='uuid' AND is_nullable='YES') THEN
            RAISE EXCEPTION 'Incompatible erasure_destroyed_keys.request_id; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destroyed_keys' AND column_name='key_handle' AND data_type='text' AND is_nullable='NO' AND collation_schema='pg_catalog' AND collation_name='C') THEN
            RAISE EXCEPTION 'Incompatible erasure_destroyed_keys.key_handle; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destroyed_keys' AND column_name='destroyed_at' AND data_type='timestamp with time zone' AND is_nullable='NO') THEN
            RAISE EXCEPTION 'Incompatible erasure_destroyed_keys.destroyed_at; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destroyed_keys' AND column_name='recorded_by' AND data_type='text' AND is_nullable='NO') THEN
            RAISE EXCEPTION 'Incompatible erasure_destroyed_keys.recorded_by; provision a new compliance schema.';
        END IF;
    END IF;
    IF to_regclass('compliance.erasure_destruction_intents') IS NOT NULL THEN
        IF (SELECT array_agg(a.attname::text ORDER BY k.ordinality) FROM pg_constraint c CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum, ordinality) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.attnum WHERE c.conrelid=to_regclass('compliance.erasure_destruction_intents') AND c.contype='p') IS DISTINCT FROM ARRAY['request_id','key_handle','key_generation']::text[] THEN
            RAISE EXCEPTION 'Incompatible erasure_destruction_intents primary key; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destruction_intents' AND column_name='request_id' AND data_type='uuid' AND is_nullable='NO') THEN
            RAISE EXCEPTION 'Incompatible erasure_destruction_intents.request_id; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destruction_intents' AND column_name='key_handle' AND data_type='text' AND is_nullable='NO' AND collation_schema='pg_catalog' AND collation_name='C') THEN
            RAISE EXCEPTION 'Incompatible erasure_destruction_intents.key_handle; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destruction_intents' AND column_name='key_generation' AND data_type='text' AND is_nullable='NO' AND collation_schema='pg_catalog' AND collation_name='C') THEN
            RAISE EXCEPTION 'Incompatible erasure_destruction_intents.key_generation; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_destruction_intents' AND column_name='staged_at' AND data_type='timestamp with time zone' AND is_nullable='NO') THEN
            RAISE EXCEPTION 'Incompatible erasure_destruction_intents.staged_at; provision a new compliance schema.';
        END IF;
    END IF;
    IF to_regclass('compliance.erasure_destroyed_keys') IS NULL THEN
        CREATE TABLE compliance.erasure_destroyed_keys (
            key_generation text COLLATE pg_catalog."C" NOT NULL,
            request_id uuid NULL,
            key_handle text COLLATE pg_catalog."C" NOT NULL,
            destroyed_at timestamp with time zone NOT NULL,
            recorded_by text NOT NULL,
            PRIMARY KEY (key_handle, key_generation)
        );
        CREATE INDEX ix_erasure_destroyed_keys_request ON compliance.erasure_destroyed_keys (request_id, key_handle);
    END IF;
    IF to_regclass('compliance.erasure_destruction_intents') IS NULL THEN
        CREATE TABLE compliance.erasure_destruction_intents (
            request_id uuid NOT NULL,
            key_handle text COLLATE pg_catalog."C" NOT NULL,
            key_generation text COLLATE pg_catalog."C" NOT NULL,
            staged_at timestamp with time zone NOT NULL,
            PRIMARY KEY (request_id, key_handle, key_generation)
        );
    END IF;
END $$;
COMMIT;
