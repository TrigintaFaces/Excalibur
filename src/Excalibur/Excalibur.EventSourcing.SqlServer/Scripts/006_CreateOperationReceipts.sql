-- Apply in the event-store database. For custom event names, rename both schema
-- and EventStoreEventsOperations to <schema>.<events-table>Operations.
IF OBJECT_ID(N'dbo.EventStoreEventsOperations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventStoreEventsOperations
    (
        OperationId binary(32) NOT NULL CONSTRAINT PK_EventStoreEventsOperations PRIMARY KEY,
        AttemptId uniqueidentifier NOT NULL,
        Fingerprint binary(32) NOT NULL,
        AggregateId nvarchar(450) NOT NULL,
        AggregateType nvarchar(450) NOT NULL,
        NextExpectedVersion bigint NOT NULL,
        FirstEventPosition bigint NOT NULL,
        LastEventPosition bigint NOT NULL,
        CommittedAt datetime2(7) NOT NULL CONSTRAINT DF_EventStoreEventsOperations_CommittedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_EventStoreEventsOperations_Positions CHECK (FirstEventPosition > 0 AND LastEventPosition >= FirstEventPosition)
    );
END;

-- Support rerunning the isolated development harness during the greenfield refactor.
IF COL_LENGTH(N'dbo.EventStoreEventsOperations', N'AttemptId') IS NULL
    ALTER TABLE dbo.EventStoreEventsOperations ADD AttemptId uniqueidentifier NOT NULL
        CONSTRAINT DF_EventStoreEventsOperations_AttemptId DEFAULT NEWID() WITH VALUES;

-- Keep this table in the TARGET database, outside source backup/restore workflows.
IF OBJECT_ID(N'dbo.EventStoreEventsSources', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventStoreEventsSources
    (
        SourceId binary(32) NOT NULL CONSTRAINT PK_EventStoreEventsSources PRIMARY KEY,
        SourceIdentity binary(32) NOT NULL,
        BoundAt datetime2(7) NOT NULL CONSTRAINT DF_EventStoreEventsSources_BoundAt DEFAULT SYSUTCDATETIME()
    );
END;
