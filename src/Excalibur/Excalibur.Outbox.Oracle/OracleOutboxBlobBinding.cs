// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using Dapper;
using Excalibur.Data;
using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>Oracle-local BLOB typing for the existing mutable request parameter bag and static binder.</summary>
internal static class OracleOutboxBlobBinding
{
    internal static DynamicParameters CreateParameters() => new BlobDynamicParameters();

    internal static void AddMessageBody(DbCommand command, byte[] value)
    {
        StaticDbCommand.AddParameter(command, "MessageBody", value, DbType.Binary);
        PromoteMessageBody(command);
    }

    private static void PromoteMessageBody(IDbCommand command)
    {
        // Set last: ODP.NET infers RAW when DbType.Binary is assigned. Explicit BLOB preserves
        // zero-length arrays as non-null empty BLOBs and keeps large payloads on the LOB path.
        if (command.Parameters["MessageBody"] is not OracleParameter parameter)
        {
            throw new InvalidOperationException("Oracle outbox MessageBody requires an OracleParameter.");
        }
        parameter.OracleDbType = OracleDbType.Blob;
    }

    private sealed class BlobDynamicParameters : DynamicParameters, SqlMapper.IDynamicParameters
    {
        void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            // Preserve the same Add/Get/ParameterNames bag, including replacements after construction.
            // Dapper populates normal parameter metadata first; only the payload's provider type changes.
            _ = StaticDbCommand.RequiredInput<byte[]>(this, "MessageBody");
            base.AddParameters(command, identity);
            PromoteMessageBody(command);
        }
    }
}
