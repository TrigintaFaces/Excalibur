// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Oracle.Requests;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>Explicit provisioning for dedicated aggregate-local transactional projection tables.</summary>
public static class OracleProjectionSchema
{
    /// <summary>Creates a new transactional destination. Run separately from application transactions because Oracle DDL commits.</summary>
    /// <param name="tableName">Physical table name including its generation suffix.</param>
    /// <param name="schemaName">Schema owning the projection table.</param>
    /// <returns>The creation script. Existing destinations are not converted or overwritten.</returns>
    public static string CreateTransactionalTableScript(string tableName, string schemaName = "EXCALIBUR")
    {
        var table = OracleTableName.Format(schemaName, tableName);
        return $"""
            CREATE TABLE {table} (
                TenantId VARCHAR2({Excalibur.Dispatch.TenantId.MaxLength} CHAR) NOT NULL,
                Id VARCHAR2(450 CHAR) NOT NULL,
                Data CLOB NOT NULL CHECK (Data IS JSON),
                LastAppliedPosition NUMBER(19) NOT NULL CHECK (LastAppliedPosition IN (-1, -2, -3)),
                TransactionalSource VARCHAR2(300 CHAR) NULL,
                TransactionalAggregateType VARCHAR2(500 CHAR) NULL,
                PRIMARY KEY (TenantId, Id)
            )
            """;
    }
}
