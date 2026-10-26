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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubscriptionCheckpoints' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[SubscriptionCheckpoints] (
        -- Collated BIN2 so the compare-and-set matches the ordinal comparison the store's contract uses.
        -- Under a case-insensitive collation two subscriptions differing only in case would silently
        -- share one checkpoint, and each would skip whatever the other had already consumed.
        [SubscriptionName] NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,

        -- The last position this subscription has PROCESSED, never the next one to fetch. Reads of the
        -- global stream are exclusive of the cursor, so the two conventions must not be mixed.
        [Position]         BIGINT NOT NULL,

        -- Operational only. Nothing reads this to make a decision; it is here so an operator can see a
        -- stalled subscription without correlating logs.
        [UpdatedAt]        DATETIMEOFFSET NOT NULL CONSTRAINT [DF_SubscriptionCheckpoints_UpdatedAt] DEFAULT SYSDATETIMEOFFSET(),

        CONSTRAINT [PK_SubscriptionCheckpoints] PRIMARY KEY CLUSTERED ([SubscriptionName])
    );
END;
GO

-- Immutable work-set binding, separate from the nullable/absent checkpoint state.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubscriptionCheckpointsDefinitions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[SubscriptionCheckpointsDefinitions] (
        -- The name may be anything the consumer chooses, so it is NVARCHAR. The hash is a SHA-256
        -- rendered as uppercase hex, which is ASCII, so it is VARCHAR. The two columns are typed
        -- DIFFERENTLY ON PURPOSE and the difference is easy to lose when transcribing this table.
        SubscriptionName NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL PRIMARY KEY,

        -- CHECKED BY CHARACTER COUNT AND ALPHABET, NOT BY BYTE LENGTH. DATALENGTH would be the
        -- natural way to write this and it is a trap: it counts BYTES, so it reads 64 for a VARCHAR
        -- hash and 128 for the identical hash in an NVARCHAR column. A consumer who retypes this
        -- column as NVARCHAR -- the obvious thing to do when the column above it is NVARCHAR -- then
        -- gets a table that can never accept a row, and learns of it as a CHECK violation at host
        -- start, naming the constraint and nothing about the cause. LEN is character-based, so this
        -- holds whichever of the two types the column ends up with.
        --
        -- The alphabet half makes the check the real contract rather than a length coincidence: the
        -- store refuses anything that is not 64 uppercase hex characters before it ever reaches SQL,
        -- and this says the same thing in the one place that survives a hand-written migration. The
        -- binary collation is what makes the lower-case range reject rather than fold.
        DefinitionHash VARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CHECK (LEN(DefinitionHash) = 64 AND DefinitionHash NOT LIKE '%[^0-9A-F]%')
    );
END;
GO
