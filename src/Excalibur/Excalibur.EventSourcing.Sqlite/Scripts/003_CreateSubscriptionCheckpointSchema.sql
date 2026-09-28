-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Durable subscription checkpoints.
--
-- SQLite is embedded, so it is tempting to treat a checkpoint as process state -- but the process is
-- exactly what restarts. Without a durable mark every subscription replays the whole stream on each
-- start, which on an append-only store grows without bound. The database file outlives the process.
--
-- The name is the key: a checkpoint is per SUBSCRIPTION, not per instance, and two processes sharing
-- one database file race for it. The store's compare-and-set advance resolves that race.

CREATE TABLE IF NOT EXISTS [SubscriptionCheckpoints] (
    -- BINARY collation so the compare-and-set matches the ordinal comparison the contract uses; under a
    -- case-insensitive collation two subscriptions differing only in case would share one checkpoint and
    -- each would skip whatever the other had consumed.
    SubscriptionName TEXT NOT NULL COLLATE BINARY,

    -- The last position PROCESSED, never the next to fetch. Global-stream reads are exclusive of the
    -- cursor, so the two conventions must not be mixed.
    Position         INTEGER NOT NULL,

    UpdatedAt        TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (SubscriptionName)
);
