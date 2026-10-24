-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
-- ddl-migration: provisioning-only {projection_table_name} - Earlier alpha projection tables require fresh provisioning and replay; CREATE does not narrow existing tenant columns.
--
-- Projection table schema.
--
-- UNLIKE 001-004, THIS ONE IS A TEMPLATE, and it has to be: a projection table's name is a
-- per-projection option, so there is no fixed set of tables to create. Replace the token below once
-- per projection, or generate the script instead:
--
--     PostgresProjectionSchema.CreateTableScript("order_summary")
--
-- Prefer the generated form. It emits this same definition from the sentinel the store itself reads,
-- so it cannot drift from the queries the store runs; this file is here because 001-004 are here and
-- this is where you looked.
--
-- WHAT THE COLUMNS ARE, AND WHY THEY ARE NOT NEGOTIABLE
--
-- The projection body is stored as JSON in a single "data" column. A table with columns named after
-- the projection type's own properties will CREATE, deploy, and then fail on the first apply --
-- against your database, where nothing in this framework can see it.
--
--   id                     the projection id. For the default keying that is the aggregate id; with a
--                          KeyedBy selector it is whatever that selector derives.
--   tenant_id              part of the primary key, so one table serves every tenant. The key is
--                          (id, tenant_id) and NOT id alone: two tenants may legitimately hold the
--                          same projection id, and keying on id alone would let one tenant's upsert
--                          overwrite another tenant's row.
--   data                   the projection, serialized as JSON. JSONB, not TEXT.
--   created_at             first write.
--   updated_at             most recent write.
--   last_applied_position  the global stream position this row was folded THROUGH. NOT NULL, and it
--                          defaults to -2 rather than 0 or NULL, both of which would be wrong:
--                            * 0 is a legitimate stream position, so a zero default would make a
--                              brand-new row claim it had already folded the first event;
--                            * NULL compares equal to nothing, and the compare-and-set that orders
--                              two concurrent writers compares against this column.
--                          -2 is the UNPLACEABLE sentinel: the row holds state whose prefix nobody
--                          established, so a positioned write refuses to advance it and only a
--                          rebuild repairs it. -1 is the separate UNNUMBERED sentinel, reachable only
--                          through an explicit unnumbered write.
--
-- COLUMN NAMES ARE LOWERCASE ON PURPOSE. The store's queries reference them unquoted, and an unquoted
-- identifier folds to lower case in Postgres -- a quoted mixed-case column would be unreachable from
-- those queries. The TABLE name is quoted by the store, so it is case-sensitive exactly as given.
--
-- THERE IS NO SCHEMA QUALIFIER, also on purpose. The store addresses the table unqualified, so it
-- resolves through search_path. Creating the table in a named schema produces a table the store
-- cannot find unless the connection's search_path includes it; set search_path on the connection
-- instead of qualifying here.

CREATE TABLE IF NOT EXISTS "{projection_table_name}" (
    id VARCHAR(450) NOT NULL,
    tenant_id VARCHAR(64) NOT NULL,
    data JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    last_applied_position BIGINT NOT NULL DEFAULT -2,
    PRIMARY KEY (id, tenant_id)
);
