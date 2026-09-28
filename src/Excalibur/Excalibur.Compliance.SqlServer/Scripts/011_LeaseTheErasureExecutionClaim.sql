-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Gives the erasure execution claim a LEASE, so a crashed executor eventually loses it.
--
-- THE PROBLEM. Claiming a request for execution is an atomic compare-and-swap from Scheduled to
-- InProgress, and that part is correct. What it lacks is an expiry. A claim is only safe if a crashed
-- holder eventually loses it, and this one could not: the executor refuses any request that is not
-- Scheduled, and the scheduler refuses to reschedule one that is InProgress -- each correct alone,
-- jointly making InProgress an ABSORBING state. An executor that died mid-erasure held the request
-- forever, with no supported operator action and no path back to a terminal state.
--
-- That is not merely untidy. Erasure destroys event payloads in place, one aggregate at a time. A crash
-- partway through leaves some payloads destroyed, the request stuck, and -- once erasure also notifies
-- read models -- the notification never sent, with the source data already gone.
--
-- THE SHAPE IS THE INBOX'S, deliberately, rather than a second invention. That store already solves
-- exactly this: the expiry is resolved by the SERVER (SYSUTCDATETIME()) so a skewed client clock cannot
-- grant itself a longer lease; a claimant takes a row whose lease has expired; and the lease value is
-- handed back as an opaque token that the completing write must still match, so an executor that paused,
-- lost its lease, and resumed cannot record a completion for work another executor has since redone.
--
-- NULL means "not claimed". Every existing row takes NULL and is claimable exactly as before, so a
-- deployment that applies this script changes no behaviour until the code that reads the column ships.

-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one with a WHERE clause),
-- and sqlcmd defaults it OFF. Without these, the filtered index below fails with Msg 1934 and is simply
-- ABSENT from the resulting database -- a script runner that does not check exit status gets a schema
-- silently missing the index the scheduler's lapsed-lease scan depends on, and the only symptom is that
-- the scan gets slower as the table grows. Matches Excalibur.Data.SqlServer/Scripts/001, which already
-- does this for the same reason.
--
-- Set before the first batch: the setting applies within its own batch and persists across later ones,
-- and these statements are executed as a single command by callers that do not split batches.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[compliance].[ErasureRequests]') AND name = N'LeaseExpiresAtUtc')
BEGIN
    ALTER TABLE [compliance].[ErasureRequests] ADD LeaseExpiresAtUtc DATETIME2(7) NULL;
END;
GO

-- Finding the requests whose lease has lapsed is a scan over status + expiry, and it runs on every
-- scheduler pass. The filter keeps the index to the rows that can actually be reclaimed.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[compliance].[ErasureRequests]') AND name = N'IX_ErasureRequests_ExpiredLease')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ErasureRequests_ExpiredLease
        ON [compliance].[ErasureRequests] (LeaseExpiresAtUtc)
        WHERE LeaseExpiresAtUtc IS NOT NULL;
END;
GO
