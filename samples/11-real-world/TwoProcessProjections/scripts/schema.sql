-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
--
-- Schema for the two-process projection sample.
--
-- The event store, checkpoint and projection tables come from the SQL Server provider's own
-- migrations -- do NOT hand-write those here, or this file drifts from the shipped schema and the
-- sample stops proving anything about the real provider. Run the provider migrations first, then
-- this script, which adds only what the sample itself owns.

IF OBJECT_ID(N'SampleEffectLog', N'U') IS NULL
BEGIN
    -- Records every DELIVERY a projection handler receives, and what it decided to do about it.
    --
    -- DELIBERATELY WITHOUT a unique constraint on EventId. A real handler owes idempotency and the
    -- natural way to provide it is exactly such a constraint -- which would make a duplicate attempt
    -- silently succeed and leave one row. This sample needs to COUNT duplicate attempts, so it
    -- records them instead. Do not copy this table into production without the constraint.
    CREATE TABLE SampleEffectLog
    (
        Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SampleEffectLog PRIMARY KEY,
        EventId     NVARCHAR(100)  NOT NULL,
        OrderId     NVARCHAR(100)  NOT NULL,
        Role        NVARCHAR(20)   NOT NULL,
        IsReplay    BIT            NOT NULL,
        -- Recorded SEPARATELY from IsReplay so the two can disagree visibly. The handler writes this
        -- row BEFORE deciding whether to perform its effect, which is the only ordering that makes a
        -- wrongly suppressed delivery countable: returning early on IsReplay meant the log could only
        -- ever hold deliveries where IsReplay was false, so the replay count was necessarily zero
        -- whatever the framework did.
        Suppressed  BIT            NOT NULL,
        AttemptedAt DATETIME2(7)   NOT NULL
    );

    CREATE INDEX IX_SampleEffectLog_EventId ON SampleEffectLog (EventId);
END;
GO

-- The projection table is NOT defined here on purpose.
--
-- Its columns are dictated by the store, not by the projection type, and a hand-written copy is how
-- this sample first failed: it declared columns named after OrderSummary's properties, deployed
-- cleanly, and then threw "Invalid column name 'Data'" at the first apply. The store ships the script
-- instead -- SqlServerProjectionSchema.CreateTableScript("SampleOrderSummary") -- and the sample runs
-- it at startup, so the table cannot drift from the store that reads it.
--
-- Nothing else is needed here: the event store, snapshot, cursor map and checkpoint tables come from
-- the provider's own migrations 001-004.
