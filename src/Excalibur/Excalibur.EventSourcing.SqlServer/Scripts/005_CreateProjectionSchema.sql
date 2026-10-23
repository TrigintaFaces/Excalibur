-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
--
-- Projection table schema.
--
-- UNLIKE 001-004, THIS ONE IS A TEMPLATE, and it has to be: a projection table's name is a
-- per-projection option, so there is no fixed set of tables to create. Replace the token below once
-- per projection, or generate the script instead:
--
--     SqlServerProjectionSchema.CreateTableScript("OrderSummary")
--
-- Prefer the generated form. It emits this same definition from the store's own schema constants, so
-- it cannot drift from the queries the store runs; this file is here because 001-004 are here and
-- this is where you looked.
--
-- WHAT THE COLUMNS ARE, AND WHY THEY ARE NOT NEGOTIABLE
--
-- The projection body is stored as JSON in a single [Data] column. A table with columns named after
-- the projection type's own properties will CREATE, deploy, and then fail on the first apply with
-- "Invalid column name 'Data'" -- against your database, where nothing in this framework can see it.
--
--   [TenantId]            part of the primary key, so one table serves every tenant. The store has no
--                         unscoped mode; a single-tenant deployment uses the reserved default tenant.
--
-- THE KEY WIDTHS ARE A BUDGET. SQL Server caps a CLUSTERED index key at 900 bytes and a primary key is
-- clustered unless told otherwise; NVARCHAR costs two bytes per character, so TenantId and Id together
-- may span at most 450 characters. TenantId is 64 because that is TenantId.MaxLength, the bound the
-- inbound boundary already enforces, so a wider column could never be filled; 64 + 350 spends 414 of
-- the 450. Widths summing past that still CREATE the
-- table, with a warning, and then refuse any insert whose actual key exceeds the limit -- a schema that
-- deploys cleanly and rejects rows later.
--   [Id]                  the projection id. For the default keying that is the aggregate id; with a
--                         KeyedBy selector it is whatever that selector derives.
--   [Data]                the projection, serialized as JSON.
--   [CreatedAt]           first write.
--   [UpdatedAt]           most recent write.
--   [LastAppliedPosition] the global stream position this row was folded THROUGH. NOT NULL, and it
--                         defaults to -2 rather than 0 or NULL, both of which would be wrong:
--                           * 0 is a legitimate stream position, so a zero default would make a brand
--                             new row claim it had already folded the first event;
--                           * NULL compares equal to nothing, and the compare-and-set that orders two
--                             concurrent writers compares against this column.
--                         -2 is the UNPLACEABLE sentinel: the row holds state whose prefix nobody
--                         established, so a positioned write refuses to advance it and only a rebuild
--                         repairs it. -1 is the separate UNNUMBERED sentinel, reachable only through an
--                         explicit unnumbered write.

IF OBJECT_ID(N'[dbo].[{ProjectionTableName}]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[{ProjectionTableName}]
    (
        [TenantId]            NVARCHAR(64)    NOT NULL,
        [Id]                  NVARCHAR(350)   NOT NULL,
        [Data]                NVARCHAR(MAX)   NOT NULL,
        [CreatedAt]           DATETIMEOFFSET  NOT NULL,
        [UpdatedAt]           DATETIMEOFFSET  NOT NULL,
        [LastAppliedPosition] BIGINT          NOT NULL
            CONSTRAINT [DF_{ProjectionTableName}_LastAppliedPosition] DEFAULT (-2),
        CONSTRAINT [PK_{ProjectionTableName}] PRIMARY KEY ([TenantId], [Id])
    );
END;
GO
