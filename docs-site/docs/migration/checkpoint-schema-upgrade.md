---
title: Upgrading legacy checkpoint tables
description: Preserve legacy checkpoint state while moving to member lifetime bindings and replaying fresh projections.
---

# Upgrading legacy checkpoint tables

The member-lifetime checkpoint schema shipped in alpha.20. It adds a binding identity and
destination to each projection's cursor, plus a reader table for generation selection.
Installing alpha.21 over an already-correct alpha.20 schema does **not** require resetting it.
An older checkpoint table cannot be upgraded by rerunning `CREATE TABLE IF NOT EXISTS`.

For SQL Server with default `dbo` names, use the tested
[alpha.19-to-alpha.20 migration](./alpha19-to-alpha20-sqlserver.md). It archives the old
checkpoint table, retains its definition table, creates the new shape, and rejects event-position
gaps before modifying the schema. The script and its tests are in
`eng/migrations/alpha19-to-alpha20/`. Do not copy an old cursor into a fresh read model.

For PostgreSQL, SQLite, Oracle, or customized names, use the explicit replacement procedure below.
It preserves the old tables and starts a new projection lifetime. This is an operational migration;
the framework does not automatically convert or delete legacy checkpoint rows.

## Prepare and stop writers

1. Back up the event database, checkpoint tables, and projection destinations. Record their names,
   subscription names, configuration, row counts, and current positions.
2. Stop **all** instances that write events, apply projections, rebuild projections, or change reader
   generations. Keep them stopped until the new configuration is deployed consistently.
3. Verify that the source retains every event required to replay from the beginning. Check that its
   global positions are unique and contiguous from 1 through the recorded head, and that any position
   allocator agrees with that head. A gap, missing history, or counter mismatch blocks this procedure.
   Investigate its provenance; do not skip the range or resequence events to make replay proceed.
4. Choose unused names for the replacement checkpoint table and its reader companion. Choose fresh
   projection destinations as well. Keep the legacy checkpoint, definition, and projection tables
   intact. An old `Definitions` table is retained evidence; current stores no longer read it.

## Provision and register the replacement

| Provider | Provisioning and configuration |
|---|---|
| PostgreSQL | Copy `Excalibur.EventSourcing.Postgres/Scripts/004_CreateSubscriptionCheckpointSchema.sql` from the package. Replace **every** `public.subscription_checkpoints` prefix with your chosen schema and new table name, including the `_readers` table and foreign key. Register `AddPostgresSubscriptionCheckpointStore(connectionFactory, schemaName, newTableName)`. |
| SQLite | Register `AddSqliteSubscriptionCheckpointStore(connectionString, newTableName)`. The store creates the new table and `newTableNameReaders` on first use. The packaged `Scripts/002_CreateSubscriptionCheckpointSchema.sql` is also available for explicit provisioning. |
| Oracle | Copy `Excalibur.EventSourcing.Oracle/Scripts/003_CreateSubscriptionCheckpointSchema.sql`. Replace **every** `EXCALIBUR.SUBSCRIPTIONCHECKPOINTS` prefix with your schema and new table name, including `READERS`, its foreign key, and index. Register `AddOracleSubscriptionCheckpointStore(connectionFactory, schemaName, newTableName)`. Oracle DDL commits implicitly: do not describe this procedure as transactionally rollbackable. If provisioning fails, inspect the new objects and finish provisioning before starting hosts. |

These are SQL identifiers chosen by the operator, not event data. Apply the provider's identifier
length limits and quoting rules. Preserve the shipped column types, binary comparison semantics,
constraints, and reader foreign key. Grant the runtime identity the required rights on the new
objects. Check that every host resolves the same checkpoint store and source subscription scope.
The generic host registration does not discover an operator's newly named table automatically.

Do not point the new store at a legacy table. Do not populate the new `BindingId`, `DefinitionHash`,
`Destination`, or `Position` columns by guessing values from the old work-set definition.
Binding a fresh member creates a new identity and a **null cursor**; that is intentional.

## Replay, verify, and cut over

Configure the new projection generation against the fresh destination and run the host to replay
from the beginning. Keep external side effects disabled or independently idempotent during replay.
Check failures and lag, compare projection contents with the event-derived expected result, and
verify that every replacement member reaches the recorded source head. A successful bind alone is
not proof that replay completed.

For readers using generation routing, switch the reader only after the replacement member reaches
the required position. A member with a null cursor must not be routable. For applications that
select destinations themselves, coordinate the equivalent reader configuration change while
traffic is stopped. Resume writers only after all instances use the replacement configuration.
Recheck new writes, projection visibility, retries, and restart behavior before removing the
maintenance window.

Retain the old tables and backups through your validation window. Never automatically drop them as
part of this upgrade. Reverting configuration after new writes requires a separate catch-up and
cutover plan: an old checkpoint does not mean the old destination contains the new events.

## What the regression tests establish

The PostgreSQL, SQLite, and Oracle checkpoint conformance suites each include
`ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows`. Against the real database engine, it
populates a legacy cursor and definition table, provisions a differently named current store,
checks that its cursor starts null, rejects a premature reader switch, advances and routes the new
binding, and verifies the legacy rows and the replacement binding survive. The SQL Server migration
suite additionally tests archival, permissions, rollback, gap refusal, and repeated execution.
These tests establish storage behavior; operators must still validate their own replayed business
projections and data before cutover.
