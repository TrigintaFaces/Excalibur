# Excalibur.Cdc.Postgres

Postgres Change Data Capture (CDC) implementation using logical replication with the pgoutput protocol.

## Schema

The CDC state store records how far each processor has read, so a processor that restarts resumes
from its last committed position instead of replaying the log. It creates its table automatically
on first use.

For a deployment that provisions schema separately, or runs without table-creation rights, the
canonical DDL ships in the package as `scripts/001_CreateCdcStateSchema.sql`. It is derived from
the statements the store issues at runtime, so a database provisioned either way has the same
shape. Defaults: schema `excalibur`, table `cdc_state` (both configurable via
`PostgresCdcStateStoreOptions`).

Run it **once** rather than from every node: PostgreSQL DDL of this shape is not concurrency-safe,
and racing `CREATE TABLE IF NOT EXISTS` statements collide rather than one quietly winning. The
script only ever creates missing objects; it does not alter an existing table.
