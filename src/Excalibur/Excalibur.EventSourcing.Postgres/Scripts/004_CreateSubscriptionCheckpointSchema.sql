-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Durable subscription checkpoints.
--
-- A catch-up subscriber reads the global stream in position order and records how far it has processed.
-- Without a durable record of that mark it replays from the beginning of the stream on every process
-- start, which for an append-only store means the work grows without bound and every projection is
-- rebuilt on every restart. The gapless global position exists precisely so that a subscriber can hold
-- one number and never look back; this table is where that number lives.
--
-- The name is the key, so a checkpoint is per SUBSCRIPTION and not per instance: two instances of the
-- same subscription share one mark and race for it, which is what the compare-and-set advance in the
-- store resolves. There is deliberately no tenant column -- a subscription reads the global stream of the
-- store it is attached to, and under tenant sharding each shard is a separate database with its own
-- position sequence and therefore its own copy of this table.

CREATE TABLE IF NOT EXISTS public.subscription_checkpoints (
    -- Compared with the C ordinal collation so the compare-and-set matches the ordinal comparison the
    -- store's contract uses. Under a linguistic collation two subscriptions differing only in case could
    -- silently share one checkpoint, and each would skip whatever the other had already consumed.
    subscription_name VARCHAR(255) COLLATE "C" NOT NULL,

    -- The last position this subscription has PROCESSED, never the next one to fetch. Reads of the
    -- global stream are exclusive of the cursor, so the two conventions must not be mixed.
    position          BIGINT NOT NULL,

    -- Operational only. Nothing reads this to make a decision; it is here so an operator can see a
    -- stalled subscription without correlating logs.
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_subscription_checkpoints PRIMARY KEY (subscription_name)
);
