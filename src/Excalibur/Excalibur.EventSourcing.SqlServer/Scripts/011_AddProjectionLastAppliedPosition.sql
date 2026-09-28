-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Adds the last-applied position to a projection table, so a projection write can be conditional on
-- which prefix of the event stream is already folded into the stored state.
--
-- WHY THE COLUMN EXISTS. Applying an event to a projection is load-modify-write, and the stored row
-- records nothing about which events are already in it. So nothing can detect a second application:
-- a handler that assigns a value survives a replay, and one that accumulates double-counts silently
-- and without bound. Recording the position makes the second application refusable.
--
-- RUN THIS ONCE PER PROJECTION TABLE. Projection tables are named by the consumer, one per projection
-- type, so this script is parameterised rather than fixed: set @TableName below and run it for each.

DECLARE @TableName SYSNAME = N'Projections';   -- <-- set this per projection table

-- ---------------------------------------------------------------------------------------------------
-- NOT NULL DEFAULT -1, AND BOTH HALVES ARE LOAD-BEARING.
--
-- NOT NULL: a nullable column would leave every existing row reporting "no position". The store reads
-- that as "no projection exists", takes the insert-if-absent branch, is refused by the primary key
-- because the row DOES exist, reports superseded, re-reads, and loops -- forever. Every projection a
-- consumer already holds would be bricked on upgrade, and a bricked projection is worse than a
-- double-counted one because nothing recovers it.
--
-- -1 RATHER THAN 0: zero is a legitimate position -- it is what a projection holding only the first
-- event carries -- so zero cannot mean "unknown". A negative sentinel is outside the domain of real
-- positions and the store maps it back to "position unknown" on read, which is distinct from both
-- "no row" and "position zero". Those are three states, and a nullable column can only express two.
-- ---------------------------------------------------------------------------------------------------

DECLARE @Sql NVARCHAR(MAX);

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(QUOTENAME(@TableName)) AND name = N'LastAppliedPosition')
BEGIN
    SET @Sql = N'ALTER TABLE ' + QUOTENAME(@TableName) +
               N' ADD LastAppliedPosition BIGINT NOT NULL CONSTRAINT DF_' + @TableName +
               N'_LastAppliedPosition DEFAULT (-1);';
    EXEC sp_executesql @Sql;
END;

-- Existing rows take the default, so they read as "position unknown" rather than as "position zero"
-- or "absent". A consumer who wants those projections to participate in conditional writes from a
-- known point must rebuild them; until then the store treats the first positioned write to such a row
-- as establishing its position.
