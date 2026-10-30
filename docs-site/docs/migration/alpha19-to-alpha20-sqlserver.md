---
title: SQL Server alpha.19 to alpha.20
description: Preserve existing data while upgrading subscription checkpoints and rebuilding projection destinations.
---

# SQL Server alpha.19 to alpha.20

The published alpha.20 release changes the durable projection checkpoint schema. Updating packages without migrating that schema will fail when the new store accesses its binding columns. Existing events and snapshots do not need rewriting for this change.

The incremental SQL Server migration and its test are in the repository at `eng/migrations/alpha19-to-alpha20/`. Read that directory's README before running `Upgrade.sql`. The script targets the default `dbo` names and preserves the complete old checkpoint table as `SubscriptionCheckpoints_Alpha19`. It creates the new `SubscriptionCheckpoints` and `SubscriptionCheckpointsReaders` tables, leaving legacy definitions, events, snapshots, CDC data, and projection contents untouched.

This is a schema migration, not an automatic transfer of projection progress. Old cursor names and set-wide hashes do not establish the new per-projection binding identity. The new application must bind and replay into fresh destinations, validate the results, and switch readers and writers together. Keep all old hosts stopped throughout the transition. See [projection evolution](../event-sourcing/projection-evolution.md) and [host-applied projections](projections-are-applied-by-the-host.md).

Reusing old positioned rows can suppress replay of events that need correction. Elasticsearch mapping corrections also require replacement indexes; SQL cannot apply those changes. Retain old indexes/tables and source history until the rebuilt generation has been verified.

Replay requires a complete global position sequence. A historical gap can remain hidden behind an old cursor and stall the new cursor when it starts from the beginning. Reads and connectivity health may remain green while Inline writes wait or application acknowledgements fail after commit. Do not retry those commands as if their events were uncommitted.

The migration now checks the actual event database under locks: positions must be unique and form `1..head`, and the singleton allocator must equal the head (zero for an empty store). Leading, interior and trailing gaps or counter inconsistencies stop the migration before checkpoint changes. The repository README includes a diagnostic query. Do not skip missing positions or renumber events automatically; establish their provenance and restore missing history or prepare a separately reviewed recovery migration first.

Ordinary Inline/Async SQL projection tables do not gain transactional columns as part of this upgrade. TransactionalInline is available starting in `10.0.0-alpha.21` and requires separately provisioned tables; it is not part of the alpha.19-to-alpha.20 schema migration.

The migration was compared against `v10.0.0-alpha.19` and the alpha.20 checkpoint schema. The target DDL was also verified against published package source commit `44c28f4ef8aead059d8d656c0b6bc3bc99d24fa3` in the `TrigintaFaces/Excalibur` release repository. Rehearse it on a restored production backup before rollout. The accompanying test checks populated legacy data, rollback, permissions, constraints, and repeat execution on SQL Server.
