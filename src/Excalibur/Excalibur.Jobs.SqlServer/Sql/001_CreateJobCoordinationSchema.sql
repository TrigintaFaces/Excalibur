-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
--
-- SQL Server Job Coordination Schema
-- Creates the tables required for distributed job coordination.
-- Default schema: [Jobs]. Override via SqlServerJobCoordinatorOptions.SchemaName.

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Jobs')
BEGIN
    EXEC('CREATE SCHEMA [Jobs]');
END
GO

-- Distributed locks for job-level mutual exclusion.
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Jobs') AND name = 'Locks')
BEGIN
    CREATE TABLE [Jobs].[Locks] (
        [JobKey]      NVARCHAR(256) NOT NULL,
        [InstanceId]  NVARCHAR(256) NOT NULL,
        [AcquiredAt]  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [ExpiresAt]   DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_Jobs_Locks] PRIMARY KEY CLUSTERED ([JobKey])
    );
END
GO

-- Registered job processing instances with heartbeat tracking.
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Jobs') AND name = 'Instances')
BEGIN
    CREATE TABLE [Jobs].[Instances] (
        [InstanceId]  NVARCHAR(256) NOT NULL,
        [HostName]    NVARCHAR(256) NOT NULL,
        [Data]        NVARCHAR(MAX) NULL,
        [HeartbeatAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [RegisteredAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Jobs_Instances] PRIMARY KEY CLUSTERED ([InstanceId])
    );
END
GO

-- Job queue for distributing work to instances.
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Jobs') AND name = 'Queue')
BEGIN
    CREATE TABLE [Jobs].[Queue] (
        [Id]               BIGINT IDENTITY(1,1) NOT NULL,
        [JobKey]           NVARCHAR(256) NOT NULL,
        [AssignedInstance] NVARCHAR(256) NOT NULL,
        [JobData]          NVARCHAR(MAX) NULL,
        [CreatedAt]        DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [Status]           INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Jobs_Queue] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Jobs_Queue_AssignedInstance_Status]
        ON [Jobs].[Queue] ([AssignedInstance], [Status]);
END
GO

-- Job completion records for auditing and coordination.
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Jobs') AND name = 'Completions')
BEGIN
    CREATE TABLE [Jobs].[Completions] (
        [Id]           BIGINT IDENTITY(1,1) NOT NULL,
        [JobKey]       NVARCHAR(256) NOT NULL,
        [InstanceId]   NVARCHAR(256) NOT NULL,
        [Success]      BIT NOT NULL,
        [ResultData]   NVARCHAR(MAX) NULL,
        [CompletedAt]  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Jobs_Completions] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Jobs_Completions_JobKey]
        ON [Jobs].[Completions] ([JobKey]);

    CREATE NONCLUSTERED INDEX [IX_Jobs_Completions_CompletedAt]
        ON [Jobs].[Completions] ([CompletedAt]);
END
GO
