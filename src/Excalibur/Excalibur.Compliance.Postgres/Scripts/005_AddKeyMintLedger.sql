-- ddl-migration: provisioning-only erasure_minted_keys - A mint ledger is the enumeration an erasure destroys from; an incompatible table is rejected rather than altered, because a row lost here is a key nothing looks for.
-- Provision the absent mint-ledger table; validate an existing table without altering records.
-- Earlier alpha schemas are unsupported: provision a new compliance schema instead.
-- All checks precede creation; a transaction-owned lock serializes this script.
BEGIN;
SELECT pg_advisory_xact_lock(728164921048);
DO $$
BEGIN
    IF to_regclass('compliance.erasure_minted_keys') IS NOT NULL THEN
        IF (SELECT array_agg(a.attname::text ORDER BY k.ordinality) FROM pg_constraint c CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum, ordinality) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.attnum WHERE c.conrelid=to_regclass('compliance.erasure_minted_keys') AND c.contype='p') IS DISTINCT FROM ARRAY['key_handle']::text[] THEN
            RAISE EXCEPTION 'Incompatible erasure_minted_keys primary key; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_minted_keys' AND column_name='key_handle' AND data_type='text' AND is_nullable='NO' AND ((collation_schema='pg_catalog' AND collation_name='C') OR (collation_name IS NULL AND (SELECT datcollate FROM pg_database WHERE datname=current_database()) IN ('C','C.UTF-8','POSIX')))) THEN
            RAISE EXCEPTION 'Incompatible erasure_minted_keys.key_handle; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_minted_keys' AND column_name='tenant_id' AND data_type='text' AND is_nullable='NO' AND ((collation_schema='pg_catalog' AND collation_name='C') OR (collation_name IS NULL AND (SELECT datcollate FROM pg_database WHERE datname=current_database()) IN ('C','C.UTF-8','POSIX')))) THEN
            RAISE EXCEPTION 'Incompatible erasure_minted_keys.tenant_id; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_minted_keys' AND column_name='data_subject_id_hash' AND data_type='text' AND is_nullable='NO' AND ((collation_schema='pg_catalog' AND collation_name='C') OR (collation_name IS NULL AND (SELECT datcollate FROM pg_database WHERE datname=current_database()) IN ('C','C.UTF-8','POSIX')))) THEN
            RAISE EXCEPTION 'Incompatible erasure_minted_keys.data_subject_id_hash; provision a new compliance schema.';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_minted_keys' AND column_name='intent_at' AND data_type='timestamp with time zone' AND is_nullable='NO') THEN
            RAISE EXCEPTION 'Incompatible erasure_minted_keys.intent_at; provision a new compliance schema.';
        END IF;
        -- minted_at MUST BE NULLABLE, and a NOT NULL one is refused rather than accepted. The null is the
        -- only thing that distinguishes a write in flight from a write that was abandoned: the row is
        -- written before the key is created, so a NOT NULL column forces every row to claim a key exists.
        -- An erasure would then read every pending row as covered and certify over a key that is about to
        -- come into existence.
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='compliance' AND table_name='erasure_minted_keys' AND column_name='minted_at' AND data_type='timestamp with time zone' AND is_nullable='YES') THEN
            RAISE EXCEPTION 'Incompatible erasure_minted_keys.minted_at; it must be NULLABLE, because a null is what marks a mint whose key creation was never confirmed. Provision a new compliance schema.';
        END IF;
        -- THE ENUMERATION INDEX IS VALIDATED, NOT ONLY CREATED. An existing table with the right primary key,
        -- columns and collations but NO index passes every check above while ListHandlesAsync scans
        -- sequentially forever, with nothing to notice. The comment on the create branch below calls this
        -- "the index the erasure cannot work without"; a validation that does not check it leaves that
        -- sentence unenforced on exactly the tables validation exists for.
        IF NOT EXISTS (
            SELECT 1 FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname='compliance' AND c.relname='erasure_minted_keys'
              AND (SELECT array_agg(a.attname::text ORDER BY k.ordinality)
                   FROM unnest(i.indkey::int[]) WITH ORDINALITY k(attnum, ordinality)
                   JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum)
                  @> ARRAY['tenant_id','data_subject_id_hash']::text[]
        ) THEN
            RAISE EXCEPTION 'erasure_minted_keys has no index covering (tenant_id, data_subject_id_hash); the erasure enumerates by that key and cannot work without it.';
        END IF;
    END IF;
    IF to_regclass('compliance.erasure_minted_keys') IS NULL THEN
        -- The handle alone is the key: existence is a per-HANDLE fact, where a destruction is a
        -- per-generation one. A generation column here would need updating after a re-mint, which makes
        -- the row mutable and rebuilds a create-or-rotate race one layer down. There is nothing to
        -- update: a handle minted, destroyed and minted again has one row throughout, and it stays true.
        --
        -- The binary collation is not stylistic. An erasure enumerates by (tenant_id,
        -- data_subject_id_hash) and the service compares handles ordinally; a database whose default
        -- collation folded case would let one owner's rows answer for another's, which is the
        -- cross-tenant direction this subsystem has already shipped once.
        CREATE TABLE compliance.erasure_minted_keys (
            key_handle text COLLATE pg_catalog."C" NOT NULL,
            tenant_id text COLLATE pg_catalog."C" NOT NULL,
            data_subject_id_hash text COLLATE pg_catalog."C" NOT NULL,
            -- WHEN THE ROW WAS WRITTEN, which is BEFORE the key was created.
            intent_at timestamp with time zone NOT NULL,
            -- WHEN THE KEY WAS CONFIRMED TO EXIST, and NULL until it was. Between the row and the key a
            -- write is invisible to both observables an erasure has -- the handle set is unchanged and the
            -- material reads absent because it genuinely is not there yet -- so that state is
            -- indistinguishable from a writer that crashed, which is safely ignorable. This column is what
            -- separates them, and an erasure that finds a null reports a concurrent write rather than
            -- certifying. DO NOT make it NOT NULL.
            minted_at timestamp with time zone NULL,
            PRIMARY KEY (key_handle)
        );
        -- The enumeration index, and the erasure cannot work without it: a handle is an opaque digest,
        -- so (tenant_id, data_subject_id_hash) is the only key a subject's handles can be found by.
        CREATE INDEX ix_erasure_minted_keys_subject ON compliance.erasure_minted_keys (tenant_id, data_subject_id_hash);
    END IF;
END $$;
COMMIT;
