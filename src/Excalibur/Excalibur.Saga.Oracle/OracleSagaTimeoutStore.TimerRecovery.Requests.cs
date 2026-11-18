// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Oracle.ManagedDataAccess.Client;

using static Excalibur.Saga.Oracle.OracleSagaTimerRecovery;

namespace Excalibur.Saga.Oracle;

internal static class OracleSagaTimerRecoveryRequests
{
    internal sealed record RecoveryTargetBinding(string Owner, string Table)
    {
        internal string Payload => Owner + "." + Table;
        internal string Targets => Owner + ".SAGATIMERRECOVERYTARGETS";
        internal string Scopes => Owner + ".SAGATIMERRECOVERYSCOPES";
        internal string Timers => Owner + ".SAGATIMERRECOVERYTIMERS";
        internal string Tokens => Owner + ".SAGATIMERRECOVERYTOKENS";
        internal string Results => Owner + ".SAGATIMERRECOVERYRESULTS";
    }
    internal static byte[] RecoveryBytes(string value)
    {
        var bytes = new byte[checked(value.Length * sizeof(char))];
        for (var index = 0; index < value.Length; index++)
        { bytes[index * 2] = (byte)value[index]; bytes[(index * 2) + 1] = (byte)(value[index] >> 8); }
        return bytes;
    }
    internal static string RecoveryText(byte[] bytes)
    {
        if ((bytes.Length & 1) != 0) { throw new InvalidDataException("Invalid ordinal identity bytes."); }
        var characters = new char[bytes.Length / 2];
        for (var index = 0; index < characters.Length; index++)
        { characters[index] = (char)(bytes[index * 2] | (bytes[(index * 2) + 1] << 8)); }
        return new string(characters);
    }
    internal static byte[] RecoveryKey(string value) => SHA256.HashData(RecoveryBytes(value));
    internal readonly record struct RecoveryParameter(string Name, OracleDbType Type, object? Value, int Size = 0);
    internal static RecoveryParameter Raw(string name, Guid value) => new(name, OracleDbType.Raw, value.ToByteArray(), 16);
    internal static RecoveryParameter Binary(string name, byte[] value) => new(name, OracleDbType.Raw, value.Length == 0 ? null : value, Math.Max(1, value.Length));
    internal static RecoveryParameter Number(string name, long value) => new(name, OracleDbType.Int64, value);
    internal static RecoveryParameter Text(string name, string? value, int size = 512) => new(name, OracleDbType.NVarchar2, value, value is null ? size : Math.Max(size, value.Length));
    internal static void BindRecovery(DbCommand command, IReadOnlyList<RecoveryParameter> parameters)
    {
        if (command is not OracleCommand oracle) { throw new InvalidOperationException("Oracle timer recovery requires the selected Oracle connection."); }
        oracle.BindByName = true;
        foreach (var value in parameters)
        {
            var parameter = new OracleParameter { ParameterName = value.Name, OracleDbType = value.Type, Value = value.Value ?? DBNull.Value };
            if (value.Size > 0) { parameter.Size = value.Size; }
            _ = command.Parameters.Add(parameter);
        }
    }
    internal sealed class RecoveryWriteRequest : DataRequestBase<IDbConnection, int>
    {
        internal RecoveryWriteRequest(string sql, RecoveryParameter[] parameters, OracleTransaction transaction, CancellationToken cancellationToken)
        {
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: cancellationToken);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command => BindRecovery(command, parameters));
        }
    }
    internal sealed class RecoveryReadRequest<T> : DataRequestBase<IDbConnection, IEnumerable<T>>
    {
        internal RecoveryReadRequest(string sql, RecoveryParameter[] parameters, OracleTransaction? transaction,
            Func<DbDataReader, T> project, CancellationToken cancellationToken)
        {
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: cancellationToken);
            ResolveAsync = connection => ReadRowsAsync(connection, command => BindRecovery(command, parameters), project);
        }
    }
    internal static long RecoveryLong(DbDataReader reader, int ordinal) => checked((long)reader.GetDecimal(ordinal));
    internal static Guid RecoveryGuid(DbDataReader reader, int ordinal) => new((byte[])reader.GetValue(ordinal));
    internal static Guid? NullableRecoveryGuid(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : RecoveryGuid(reader, ordinal);
    internal static string? NullableRecoveryText(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    internal static Task<int> ExecuteRecoveryAsync(OracleConnection connection, OracleTransaction transaction,
        string sql, RecoveryParameter[] parameters, CancellationToken cancellationToken) =>
        connection.ResolveAsync(new RecoveryWriteRequest(sql, parameters, transaction, cancellationToken));
    internal static Task<IEnumerable<T>> ReadRecoveryAsync<T>(OracleConnection connection, OracleTransaction? transaction,
        string sql, RecoveryParameter[] parameters, Func<DbDataReader, T> project, CancellationToken cancellationToken) =>
        connection.ResolveAsync(new RecoveryReadRequest<T>(sql, parameters, transaction, project, cancellationToken));

    internal static async Task<bool> RecoveryMetadataAvailableAsync(OracleConnection connection, OracleTransaction? transaction,
        RecoveryTargetBinding target, CancellationToken cancellationToken)
    {
        var counts = (await ReadRecoveryAsync(connection, transaction,
            "SELECT (SELECT COUNT(*) FROM ALL_OBJECTS WHERE OWNER=:Owner AND OBJECT_NAME=:Payload AND OBJECT_TYPE='TABLE'),"
            + "(SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER=:Owner AND TABLE_NAME IN ('SAGATIMERRECOVERYTARGETS','SAGATIMERRECOVERYSCOPES','SAGATIMERRECOVERYTIMERS','SAGATIMERRECOVERYTOKENS','SAGATIMERRECOVERYRESULTS')),"
            + "(SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER=:Owner AND TABLE_NAME=:Payload AND COLUMN_NAME IN ('OWNEDINCARNATIONID','OWNEDGENERATION','OWNEDSTATETYPE','RECOVERYSAGAGUID')) FROM DUAL",
            [Text("Owner", target.Owner, 128), Text("Payload", target.Table, 128)],
            reader => (Direct: RecoveryLong(reader, 0), Tables: RecoveryLong(reader, 1), Columns: RecoveryLong(reader, 2)), cancellationToken).ConfigureAwait(false)).Single();
        if (counts.Direct != 1)
        { throw new InvalidOperationException("Oracle timer recovery requires the selected direct payload table, not a view or synonym."); }
        // Shared metadata does not enroll an unrelated selected payload table. However a
        // REGISTERED table cannot downgrade to legacy by dropping all of its owned columns.
        if (counts.Columns == 0)
        {
            if (counts.Tables == 0) { return false; }
            if (counts.Tables != 5)
            { throw new InvalidOperationException("Apply the complete Oracle 03 ordinary timer recovery schema for the selected table."); }
            var registered = (await ReadRecoveryAsync(connection, transaction,
                $"SELECT COUNT(*) FROM {target.Targets} t JOIN ALL_OBJECTS o ON t.ObjectId=o.OBJECT_ID WHERE o.OWNER=:Owner AND o.OBJECT_NAME=:Payload AND o.OBJECT_TYPE='TABLE'",
                [Text("Owner", target.Owner, 128), Text("Payload", target.Table, 128)],
                reader => RecoveryLong(reader, 0), cancellationToken).ConfigureAwait(false)).Single();
            if (registered != 0)
            { throw new InvalidOperationException("The registered Oracle 03 receiver target is missing its owned columns; restore the complete selected schema before mutation."); }
            return false;
        }
        if (counts.Tables != 5 || counts.Columns != 4)
        { throw new InvalidOperationException("Apply the complete Oracle 03 ordinary timer recovery schema for the selected table."); }
        return true;
    }
    internal static async Task<Guid> RequireRecoveryTargetAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, CancellationToken cancellationToken)
    {
        if (!await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false))
        { throw new InvalidOperationException("Apply Oracle 03 ordinary timer recovery metadata before timer-owning state can commit."); }
        var rows = await ReadRecoveryAsync(connection, transaction,
            $"SELECT t.TargetId,t.SchemaVersion FROM {target.Targets} t JOIN ALL_OBJECTS o ON t.ObjectId=o.OBJECT_ID AND t.DataObjectId=NVL(o.DATA_OBJECT_ID,0) WHERE o.OWNER=:Owner AND o.OBJECT_NAME=:Payload AND o.OBJECT_TYPE='TABLE'",
            [Text("Owner", target.Owner, 128), Text("Payload", target.Table, 128)],
            reader => (Id: RecoveryGuid(reader, 0), Version: RecoveryLong(reader, 1)), cancellationToken).ConfigureAwait(false);
        var selected = rows.SingleOrDefault();
        if (selected.Id == Guid.Empty || selected.Version != 1)
        { throw new InvalidOperationException("Register the exact selected direct Oracle timeout table in recovery schema 03; aliases or recreated targets are not adopted implicitly."); }
        return selected.Id;
    }
    internal static async Task<RecoveryScopeRow?> ReadRecoveryScopeAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, Guid selected, SagaTimerRecoveryDomain domain, CancellationToken cancellationToken)
    {
        var rows = await ReadRecoveryAsync(connection, transaction,
            $"SELECT ScopeId,TenantBytes,SagaGuid,Token,IncarnationId,SequenceNumber,MutationRevision,StateType,BatchId,ExpectedFence,ObservedRevision,Digest,CancelAllThrough,CancelAllRevision FROM {target.Scopes} WHERE TargetId=:Target AND TenantKey=:TenantKey AND SagaGuid=:Saga FOR UPDATE",
            [Raw("Target", selected), Binary("TenantKey", RecoveryKey(domain.TenantId)), Raw("Saga", domain.SagaId)], reader =>
            {
                if (!((byte[])reader.GetValue(1)).AsSpan().SequenceEqual(RecoveryBytes(domain.TenantId)))
                { throw new InvalidOperationException("Exact Oracle receiver tenant differs from its retained domain key."); }
                return new RecoveryScopeRow(RecoveryGuid(reader, 0), selected, domain.TenantId, RecoveryGuid(reader, 2),
                    RecoveryGuid(reader, 3), RecoveryGuid(reader, 4), RecoveryLong(reader, 5), RecoveryLong(reader, 6), NullableRecoveryText(reader, 7),
                    NullableRecoveryGuid(reader, 8), NullableRecoveryGuid(reader, 9), reader.IsDBNull(10) ? null : RecoveryLong(reader, 10),
                    reader.IsDBNull(11) ? [] : (byte[])reader.GetValue(11), RecoveryLong(reader, 12), RecoveryLong(reader, 13));
            }, cancellationToken).ConfigureAwait(false);
        return rows.SingleOrDefault();
    }
    internal static async Task CreateRecoveryScopeAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, RecoveryScopeRow scope, CancellationToken cancellationToken)
    {
        _ = await ExecuteRecoveryAsync(connection, transaction,
            $"INSERT INTO {target.Scopes}(ScopeId,TargetId,TenantKey,TenantBytes,SagaGuid,Token,IncarnationId,SequenceNumber,MutationRevision,CancelAllThrough,CancelAllRevision) VALUES(:Id,:Target,:TenantKey,:TenantBytes,:Saga,:Token,:Empty,0,0,-1,0)",
            [Raw("Id", scope.Id), Raw("Target", scope.Target), Binary("TenantKey", RecoveryKey(scope.Tenant)), Binary("TenantBytes", RecoveryBytes(scope.Tenant)),
                Raw("Saga", scope.SagaId), Raw("Token", scope.Token), Raw("Empty", Guid.Empty)], cancellationToken).ConfigureAwait(false);
    }
    internal static async Task<RecoveryTimerRow?> ReadRecoveryTimerAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, Guid scope, string timeoutId, CancellationToken cancellationToken)
    {
        var rows = await ReadRecoveryAsync(connection, transaction,
            $"SELECT IdBytes,Tracked,IncarnationId,SequenceNumber,StateType,CreatedRevision,IsCancelled,CancelThrough,CancelRevision FROM {target.Timers} WHERE ScopeId=:Scope AND IdKey=:IdKey",
            [Raw("Scope", scope), Binary("IdKey", RecoveryKey(timeoutId))], reader =>
            {
                if (!((byte[])reader.GetValue(0)).AsSpan().SequenceEqual(RecoveryBytes(timeoutId)))
                { throw new InvalidOperationException("Timer identity differs from its retained exact key."); }
                return new RecoveryTimerRow(RecoveryLong(reader, 1) == 1, NullableRecoveryGuid(reader, 2), reader.IsDBNull(3) ? null : RecoveryLong(reader, 3),
                    NullableRecoveryText(reader, 4), RecoveryLong(reader, 5), RecoveryLong(reader, 6) == 1, RecoveryLong(reader, 7), RecoveryLong(reader, 8));
            }, cancellationToken).ConfigureAwait(false);
        return rows.SingleOrDefault();
    }
    internal static Task<IEnumerable<int>> ReadRetiredRecoveryTokenAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, Guid scope, Guid token, CancellationToken cancellationToken) =>
        ReadRecoveryAsync(connection, transaction, $"SELECT 1 FROM {target.Tokens} WHERE ScopeId=:Scope AND Token=:Token",
            [Raw("Scope", scope), Raw("Token", token)], _ => 1, cancellationToken);
    internal static async Task RetireRecoveryTokenAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, Guid scope, Guid token, CancellationToken cancellationToken)
    {
        _ = await ExecuteRecoveryAsync(connection, transaction, $"INSERT INTO {target.Tokens}(ScopeId,Token) VALUES(:Scope,:Token)",
            [Raw("Scope", scope), Raw("Token", token)], cancellationToken).ConfigureAwait(false);
    }
    internal static Task<IEnumerable<SagaTimerRecoveryIntentResult>> ReadRecoveryResultsAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, Guid scope, CancellationToken cancellationToken) =>
        ReadRecoveryAsync(connection, transaction, $"SELECT IntentOrdinal,ActionKind,IdBytes,OutcomeKind,AuthorityRevision FROM {target.Results} WHERE ScopeId=:Scope ORDER BY IntentOrdinal",
            [Raw("Scope", scope)], reader => new SagaTimerRecoveryIntentResult(checked((int)RecoveryLong(reader, 0)),
                (SagaPreparedTimerAction)RecoveryLong(reader, 1), reader.IsDBNull(2) ? string.Empty : RecoveryText((byte[])reader.GetValue(2)),
                (SagaTimerRecoveryIntentOutcome)RecoveryLong(reader, 3), RecoveryLong(reader, 4)), cancellationToken);
    internal static async Task WriteRecoveryReceiptAsync(OracleConnection connection, OracleTransaction transaction, RecoveryTargetBinding target,
        Guid scope, Guid token, SagaTimerRecoveryJournal journal, byte[] digest, long revision, CancellationToken cancellationToken)
    {
        _ = await ExecuteRecoveryAsync(connection, transaction,
            $"UPDATE {target.Scopes} SET Token=:Token,IncarnationId=:Incarnation,SequenceNumber=:Sequence,MutationRevision=:Revision,StateType=:StateType,BatchId=:Batch,ExpectedFence=:Expected,ObservedRevision=:Observed,Digest=:Digest WHERE ScopeId=:Scope",
            [Raw("Token", token), Raw("Incarnation", journal.IncarnationId), Number("Sequence", journal.Sequence), Number("Revision", revision), Text("StateType", journal.Scope.StateType, 500),
                Raw("Batch", journal.BatchId), Raw("Expected", journal.ExpectedFence), Number("Observed", journal.ObservedRevision), Binary("Digest", digest), Raw("Scope", scope)], cancellationToken).ConfigureAwait(false);
        _ = await ExecuteRecoveryAsync(connection, transaction, $"DELETE FROM {target.Results} WHERE ScopeId=:Scope", [Raw("Scope", scope)], cancellationToken).ConfigureAwait(false);
    }
    internal static async Task WriteRecoveryResultAsync(OracleConnection connection, OracleTransaction transaction, RecoveryTargetBinding target,
        Guid scope, SagaTimerRecoveryIntentResult result, CancellationToken cancellationToken)
    {
        _ = await ExecuteRecoveryAsync(connection, transaction,
            $"INSERT INTO {target.Results}(ScopeId,IntentOrdinal,ActionKind,IdBytes,OutcomeKind,AuthorityRevision) VALUES(:Scope,:Ordinal,:Action,:IdBytes,:Outcome,:Revision)",
            [Raw("Scope", scope), Number("Ordinal", result.Ordinal), Number("Action", (int)result.Action), Binary("IdBytes", RecoveryBytes(result.TimeoutId)),
                Number("Outcome", (int)result.Outcome), Number("Revision", result.AuthorityRevision)], cancellationToken).ConfigureAwait(false);
    }
}
