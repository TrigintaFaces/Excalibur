# Excalibur.Cdc.Postgres

Postgres Change Data Capture (CDC) implementation using logical replication with the pgoutput protocol.

## Schema

The CDC state store records how far each processor has read, for monitoring and for
`GetCurrentPositionAsync`. A processor that restarts resumes from its replication slot, which
PostgreSQL advances only after a transaction's changes were handed to your handler, so it never resumes
past a change that was not delivered; handlers must be idempotent because a change can be delivered
again. The store creates its table automatically on first use.

For a deployment that provisions schema separately, or runs without table-creation rights, the
canonical DDL ships in the package as `scripts/001_CreateCdcStateSchema.sql`. It is derived from
the statements the store issues at runtime, so a database provisioned either way has the same
shape. Defaults: schema `excalibur`, table `cdc_state` (both configurable via
`PostgresCdcStateStoreOptions`).

Run it **once** rather than from every node: PostgreSQL DDL of this shape is not concurrency-safe,
and racing `CREATE TABLE IF NOT EXISTS` statements collide rather than one quietly winning. The
script only ever creates missing objects; it does not alter an existing table.


## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
