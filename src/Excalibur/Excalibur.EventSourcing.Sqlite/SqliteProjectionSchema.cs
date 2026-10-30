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
        return $"""
            CREATE TABLE IF NOT EXISTS [{tableName}] (
                TenantId TEXT COLLATE BINARY NOT NULL,
                Id TEXT COLLATE BINARY NOT NULL,
                Data TEXT NOT NULL CHECK (json_valid(Data)),
                LastAppliedPosition INTEGER NOT NULL CHECK (LastAppliedPosition IN (-1, -2, -3)),
                TransactionalSource TEXT COLLATE BINARY NULL,
                TransactionalAggregateType TEXT COLLATE BINARY NULL,
                PRIMARY KEY (TenantId, Id)
            );
            """;
    }
}
