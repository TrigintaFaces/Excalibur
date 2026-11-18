-- Apply after 004_CreateScheduleSchema.sql and 005_CreateScheduleFailureSchema.sql.
-- Nullable primitives deliberately have no backfill/default: an old transition-zone
-- row without an explicit declaration must remain visible and refuse NEW admission.
IF COL_LENGTH(N'dispatch.ScheduledMessage', N'CronRecurrenceProfileId') IS NULL
    ALTER TABLE dispatch.ScheduledMessage ADD CronRecurrenceProfileId nvarchar(max) NULL;
IF COL_LENGTH(N'dispatch.ScheduledMessage', N'CronRecurrenceProfileVersion') IS NULL
    ALTER TABLE dispatch.ScheduledMessage ADD CronRecurrenceProfileVersion int NULL;
IF COL_LENGTH(N'dispatch.ScheduleOccurrenceFailure', N'CronRecurrenceProfileId') IS NULL
    ALTER TABLE dispatch.ScheduleOccurrenceFailure ADD CronRecurrenceProfileId nvarchar(max) NULL;
IF COL_LENGTH(N'dispatch.ScheduleOccurrenceFailure', N'CronRecurrenceProfileVersion') IS NULL
    ALTER TABLE dispatch.ScheduleOccurrenceFailure ADD CronRecurrenceProfileVersion int NULL;
