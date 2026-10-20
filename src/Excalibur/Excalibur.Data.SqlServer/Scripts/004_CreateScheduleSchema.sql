-- Store-global schedule administration. MetadataJson contains optional message envelope
-- metadata, not a tenant partition or an authorization boundary.
IF SCHEMA_ID(N'dispatch') IS NULL EXEC(N'CREATE SCHEMA dispatch');
IF OBJECT_ID(N'dispatch.ScheduleAuthority', N'U') IS NULL
BEGIN
    CREATE TABLE dispatch.ScheduleAuthority
    (
        SingletonId tinyint NOT NULL CONSTRAINT PK_ScheduleAuthority PRIMARY KEY,
        StoreId uniqueidentifier NOT NULL,
        CONSTRAINT CK_ScheduleAuthority_Singleton CHECK (SingletonId = 1)
    );
    INSERT dispatch.ScheduleAuthority (SingletonId, StoreId) VALUES (1, NEWID());
END;
IF OBJECT_ID(N'dispatch.ScheduledMessage', N'U') IS NULL
BEGIN
    CREATE TABLE dispatch.ScheduledMessage
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ScheduledMessage PRIMARY KEY,
        Generation uniqueidentifier NOT NULL,
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
        OwnerToken uniqueidentifier NULL,
        AdmittedOccurrence datetimeoffset(7) NULL,
        LeaseExpiresUtc datetimeoffset(7) NULL,
        ProgressCommitted bit NOT NULL CONSTRAINT DF_ScheduledMessage_Progress DEFAULT 0,
        CONSTRAINT CK_ScheduledMessage_Owner CHECK
        ((OwnerToken IS NULL AND AdmittedOccurrence IS NULL AND LeaseExpiresUtc IS NULL)
         OR (OwnerToken IS NOT NULL AND AdmittedOccurrence IS NOT NULL AND LeaseExpiresUtc IS NOT NULL))
    );
END;
