-- Apply after 004_CreateScheduleSchema.sql. No runtime schema creation.
--
-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one carrying a WHERE).
-- UX_ScheduleOccurrenceFailure_Command below is filtered. sqlcmd connects with QUOTED_IDENTIFIER
-- OFF, so without this the CREATE fails with Msg 1934 and the index is simply ABSENT -- the script
-- otherwise appears to succeed, and the uniqueness it is there to enforce silently does not exist.
SET QUOTED_IDENTIFIER ON;
IF COL_LENGTH(N'dispatch.ScheduledMessage', N'CapturedMaxAdmissionAttempts') IS NULL
    ALTER TABLE dispatch.ScheduledMessage ADD CapturedMaxAdmissionAttempts int NULL;
IF OBJECT_ID(N'dispatch.ScheduleOccurrenceFailure', N'U') IS NULL
BEGIN
    CREATE TABLE dispatch.ScheduleOccurrenceFailure
    (
        StoreId uniqueidentifier NOT NULL,
        Id uniqueidentifier NOT NULL,
        Generation uniqueidentifier NOT NULL,
        FailureOccurrenceUtc datetimeoffset(7) NOT NULL,
        QuarantineId uniqueidentifier NOT NULL,
        QuarantineVersion bigint NOT NULL,
        Reason int NOT NULL,
        Disposition int NOT NULL,
        RecordedUtc datetimeoffset(7) NOT NULL,
        LastFailureOwner uniqueidentifier NOT NULL,
        LastFailureReason int NOT NULL,
        MessageName nvarchar(max) NOT NULL,
        MessageBody nvarchar(max) NOT NULL,
        CronExpression nvarchar(max) NULL,
        TimeZoneId nvarchar(max) NULL,
        IntervalTicks bigint NULL,
        NextExecutionUtc datetimeoffset(7) NULL,
        LastExecutionUtc datetimeoffset(7) NULL,
        Enabled bit NOT NULL,
        MissedExecutionBehavior int NULL,
        MetadataJson nvarchar(max) NOT NULL,
        -- COLLATE pins tenant comparison to a binary collation. Without it the column inherits the
        -- server default, which is case-insensitive on a stock install, so 'Acme' and 'acme' compare
        -- EQUAL and two distinct tenants can read each other's rows.
        TenantId nvarchar(max) COLLATE Latin1_General_BIN2 NULL,
        OwnerToken uniqueidentifier NULL,
        AdmittedOccurrence datetimeoffset(7) NULL,
        LeaseExpiresUtc datetimeoffset(7) NULL,
        ProgressCommitted bit NOT NULL,
        AdmissionAttempts int NOT NULL,
        CapturedMaxAdmissionAttempts int NOT NULL,
        RecoveryCommandId uniqueidentifier NULL,
        RecoveryGeneration uniqueidentifier NULL,
        RecoveryRecordedUtc datetimeoffset(7) NULL,
        QuarantineOrdinal AS
            (LOWER(REPLACE(CONVERT(char(36), QuarantineId), '-', '')) COLLATE Latin1_General_100_BIN2) PERSISTED,
        CONSTRAINT PK_ScheduleOccurrenceFailure PRIMARY KEY (StoreId, Id, Generation, FailureOccurrenceUtc),
        CONSTRAINT UQ_ScheduleOccurrenceFailure_Quarantine UNIQUE (QuarantineId),
        CONSTRAINT CK_ScheduleOccurrenceFailure_Count CHECK (AdmissionAttempts >= 0 AND CapturedMaxAdmissionAttempts > 0),
        CONSTRAINT CK_ScheduleOccurrenceFailure_State CHECK (Disposition IN (0,1) AND Reason BETWEEN 0 AND 7 AND LastFailureReason BETWEEN 0 AND 7 AND QuarantineVersion > 0),
        CONSTRAINT CK_ScheduleOccurrenceFailure_Receipt CHECK
        ((RecoveryCommandId IS NULL AND RecoveryGeneration IS NULL AND RecoveryRecordedUtc IS NULL)
         OR (RecoveryCommandId IS NOT NULL AND RecoveryGeneration IS NOT NULL AND RecoveryRecordedUtc IS NOT NULL AND Disposition=1))
    );
    CREATE UNIQUE INDEX UX_ScheduleOccurrenceFailure_Command
        ON dispatch.ScheduleOccurrenceFailure (RecoveryCommandId) WHERE RecoveryCommandId IS NOT NULL;
    CREATE INDEX IX_ScheduleOccurrenceFailure_Ordinal
        ON dispatch.ScheduleOccurrenceFailure (StoreId, Disposition, QuarantineOrdinal);
    CREATE INDEX IX_ScheduleOccurrenceFailure_Recovery
        ON dispatch.ScheduleOccurrenceFailure (StoreId, Id, RecoveryGeneration, FailureOccurrenceUtc);
END;
