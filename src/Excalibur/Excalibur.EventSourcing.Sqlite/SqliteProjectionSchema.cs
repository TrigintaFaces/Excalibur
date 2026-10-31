// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Validation;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>Explicit provisioning for dedicated aggregate-local transactional projection tables.</summary>
public static class SqliteProjectionSchema
{
    /// <summary>Creates a transactional destination, preserving any existing table unchanged.</summary>
    /// <param name="tableName">Physical table name including any projection generation suffix.</param>
    /// <returns>The creation script. Existing ordinary destinations must not be reused.</returns>
    public static string CreateTransactionalTableScript(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        SqlIdentifierValidator.ThrowIfInvalid(tableName, nameof(tableName));

        // THE DEFAULT IS -2 AND BOTH HALVES OF THAT MATTER. The PostgreSQL and SQL Server scripts have
        // carried it from the start and state the reasoning; these two providers shipped the column with
        // NO default, so the same hand-written INSERT a consumer runs to seed or backfill a destination
        // succeeded there and failed here. NOT NULL keeps a row out of a state the compare-and-set
        // cannot compare against, because NULL compares equal to nothing. -2 rather than 0 because zero
        // is a legitimate stream position, so a zero default would make a brand-new row claim it had
        // already folded the first event; -2 is the UNPLACEABLE sentinel, which a positioned write
        // refuses to advance until a rebuild establishes a real prefix.
        return $"""
            CREATE TABLE IF NOT EXISTS [{tableName}] (
                TenantId TEXT COLLATE BINARY NOT NULL,
                Id TEXT COLLATE BINARY NOT NULL,
                Data TEXT NOT NULL CHECK (json_valid(Data)),
                LastAppliedPosition INTEGER NOT NULL DEFAULT -2 CHECK (LastAppliedPosition >= 0 OR LastAppliedPosition IN (-1, -2, -3)),
                TransactionalSource TEXT COLLATE BINARY NULL,
                TransactionalAggregateType TEXT COLLATE BINARY NULL,
                PRIMARY KEY (TenantId, Id)
            );
            """;
    }
}
