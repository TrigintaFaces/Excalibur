-- PostgreSQL Migration for Excalibur.EventSourcing.Postgres — SNAPSHOT STORE
-- Version: 1.0
--
-- Converges the snapshot store's untenanted representation onto the reserved
-- '__untenanted__' sentinel, matching the event store and the other providers.
--
-- BEFORE: an untenanted snapshot carries the empty string ''.
-- AFTER:  an untenanted snapshot carries '__untenanted__'.
--
-- The column is unchanged: it was already VARCHAR(255) NOT NULL and already part of the
-- primary key. Only the VALUE moves. There is no nullability change and no index change on
-- this provider — the empty string was expressible here, which is precisely why the
-- divergence survived: it worked locally and only failed on contact with Oracle, where
-- '' IS NULL.
--
-- Run 001_CreateSnapshotSchema.sql for NEW deployments. This script is only for a schema
-- created by an earlier revision that still holds '' tenants.
--
-- ---------------------------------------------------------------------------------------
-- STEP 0 — PRE-FLIGHT. RUN THIS FIRST AND READ THE RESULT. DO NOT SKIP.
-- ---------------------------------------------------------------------------------------
--
-- The rewrite is safe in the general case: '' and '__untenanted__' are both ordinary
-- non-null values here, so each already occupies its own primary-key slot, and rewriting
-- one onto the other preserves row identity for every aggregate that has only one of them.
-- No real tenant can occupy the sentinel: a scoped tenant that names it is rejected before
-- it reaches the database.
--
-- There is exactly ONE case where that does not hold. A table already holding BOTH a ''
-- row AND a '__untenanted__' row for the same (aggregate_id, aggregate_type) has two
-- DISTINCT primary keys today. After the rewrite they collide and the UPDATE fails on the
-- primary key.
--
-- That failure is CORRECT — it means the data already holds two rows each claiming to be
-- the same untenanted snapshot, and only an operator can decide which survives. But it must
-- surface HERE, as a query, not as a partially-applied UPDATE.
--
-- Expected result: NO ROWS. Any row returned is a genuine data conflict — resolve it before
-- continuing. Do not proceed on a non-empty result.

SELECT aggregate_id,
       aggregate_type,
       COUNT(*) AS colliding_rows
  FROM public.event_store_snapshots
 WHERE tenant_id IN ('', '__untenanted__')
 GROUP BY aggregate_id, aggregate_type
HAVING COUNT(*) > 1;

-- ---------------------------------------------------------------------------------------
-- STEP 1 — Rewrite the empty-string tenant onto the sentinel.
-- ---------------------------------------------------------------------------------------
-- Only rows that are genuinely untenanted are touched. A row carrying a real tenant is not
-- matched by this predicate and is left exactly as it is.
--
-- Wrapped in a transaction: on the collision the pre-flight is meant to catch, this rolls
-- back whole rather than leaving the table split between two representations — which would
-- be strictly worse than the single divergence being repaired.

BEGIN;

UPDATE public.event_store_snapshots
   SET tenant_id = '__untenanted__'
 WHERE tenant_id = '';

COMMIT;

-- ---------------------------------------------------------------------------------------
-- STEP 2 — Verify. Expected result: NO ROWS.
-- ---------------------------------------------------------------------------------------
-- No schema change is required on this provider, so this query is the whole of the
-- post-condition: no untenanted row may still carry the old representation.

SELECT COUNT(*) AS remaining_empty_tenants
  FROM public.event_store_snapshots
 WHERE tenant_id = ''
HAVING COUNT(*) > 0;
