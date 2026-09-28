-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Adds the last-applied position to a projection table, so a projection write can be conditional on
-- which prefix of the event stream is already folded into the stored state.
--
-- WHY THE COLUMN EXISTS. Applying an event to a projection is load-modify-write, and the stored row
-- records nothing about which events are already in it, so nothing can detect a second application.
-- An assigning handler survives a replay; an accumulating one double-counts, silently and without
-- bound. Recording the position makes the second application refusable.
--
-- RUN ONCE PER PROJECTION TABLE. Projection tables are named by the consumer, one per projection type,
-- so replace the table name below and run it for each.

DO $$
DECLARE
    target_table TEXT := 'projections';   -- <-- set this per projection table
BEGIN
    -- -----------------------------------------------------------------------------------------------
    -- NOT NULL DEFAULT -1, AND BOTH HALVES ARE LOAD-BEARING.
    --
    -- NOT NULL: a nullable column leaves every existing row reporting "no position". The store reads
    -- that as "no projection exists", takes the insert-if-absent branch, is refused by the primary key
    -- because the row DOES exist, reports superseded, re-reads, and loops forever. Every projection a
    -- consumer already holds would be bricked on upgrade, and a bricked projection is worse than a
    -- double-counted one because nothing recovers it.
    --
    -- -1 RATHER THAN 0: zero is a legitimate position -- a projection holding only the first event
    -- carries it -- so zero cannot mean "unknown". A negative sentinel sits outside the domain of real
    -- positions, and the store maps it back to "position unknown" on read. That is a third state,
    -- distinct from "no row" and from "position zero", and a nullable column can express only two.
    -- -----------------------------------------------------------------------------------------------
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = target_table AND column_name = 'last_applied_position')
    THEN
        EXECUTE format(
            'ALTER TABLE %I ADD COLUMN last_applied_position BIGINT NOT NULL DEFAULT -1;',
            target_table);
    END IF;
END $$;

-- Existing rows take the default and therefore read as "position unknown" rather than as position zero
-- or as absent. The first positioned write to such a row establishes its position; a consumer who needs
-- those projections positioned from a known point rebuilds them.
