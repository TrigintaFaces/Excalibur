// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.EventSourcing.Oracle.Requests;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>Explicit provisioning for dedicated aggregate-local transactional projection tables.</summary>
public static class OracleProjectionSchema
{
    /// <summary>The longest table name this script can name a primary key for.</summary>
    /// <remarks>
    /// Oracle allows 128-byte identifiers from 12.2, which is already this script's floor because it uses
    /// <c>IS JSON</c> and the stores compare with <c>COLLATE BINARY</c>. The constraint name is the table
    /// name plus a three-character prefix, so the table is bounded three characters below the identifier
    /// limit. The bound is CHECKED rather than absorbed by truncation: two destinations sharing a 125-
    /// character prefix would otherwise derive one constraint name, and the second CREATE TABLE would
    /// fail with a duplicate-name error naming neither of them.
    /// </remarks>
    private const int MaxTableNameLength = 125;

    /// <summary>Creates a new transactional destination. Run separately from application transactions because Oracle DDL commits.</summary>
    /// <param name="tableName">Physical table name including its generation suffix.</param>
    /// <param name="schemaName">Schema owning the projection table.</param>
    /// <returns>The creation script. Existing destinations are not converted or overwritten.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tableName"/> is too long for a named primary key to be derived from it.
    /// </exception>
    public static string CreateTransactionalTableScript(string tableName, string schemaName = "EXCALIBUR")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (tableName.Length > MaxTableNameLength)
        {
            throw new ArgumentException(
                $"A projection table name may be at most {MaxTableNameLength.ToString(CultureInfo.InvariantCulture)} "
                + "characters so its primary key can be named within Oracle's identifier limit; "
                + $"'{tableName}' is {tableName.Length.ToString(CultureInfo.InvariantCulture)}.",
                nameof(tableName));
        }

        var table = OracleTableName.Format(schemaName, tableName);

        // THE PRIMARY KEY IS NAMED, and the reason is diagnosis rather than behaviour. Declared inline
        // and unnamed, Oracle assigns SYS_Cnnnnnn, so a constraint violation names a number that appears
        // in no script, no migration and no document a consumer holds -- for the one table whose key
        // collision is a routine, expected outcome of concurrent writers. Naming it makes the message
        // legible. It is deliberately NOT load-bearing: the stores separate this key's violation from any
        // other unique constraint by looking for the row, never by parsing a name out of an error.
        var primaryKey = "PK_" + tableName.ToUpperInvariant();

        // THE DEFAULT IS -2 AND BOTH HALVES OF THAT MATTER. The PostgreSQL and SQL Server scripts have
        // carried it from the start and state the reasoning; these two providers shipped the column with
        // NO default, so the same hand-written INSERT a consumer runs to seed or backfill a destination
        // succeeded there and failed here. NOT NULL keeps a row out of a state the compare-and-set
        // cannot compare against, because NULL compares equal to nothing. -2 rather than 0 because zero
        // is a legitimate stream position, so a zero default would make a brand-new row claim it had
        // already folded the first event; -2 is the UNPLACEABLE sentinel, which a positioned write
        // refuses to advance until a rebuild establishes a real prefix.
        return $"""
            CREATE TABLE {table} (
                TenantId VARCHAR2({Excalibur.Dispatch.TenantId.MaxLength} CHAR) NOT NULL,
                Id VARCHAR2(450 CHAR) NOT NULL,
                Data CLOB NOT NULL CHECK (Data IS JSON),
                LastAppliedPosition NUMBER(19) DEFAULT -2 NOT NULL CHECK (LastAppliedPosition >= 0 OR LastAppliedPosition IN (-1, -2, -3)),
                TransactionalSource VARCHAR2(300 CHAR) NULL,
                TransactionalAggregateType VARCHAR2(500 CHAR) NULL,
                CONSTRAINT {primaryKey} PRIMARY KEY (TenantId, Id)
            )
            """;
    }
}
