// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using Excalibur.Data;
using System.Security.Cryptography;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Oracle.ManagedDataAccess.Client;
using static Excalibur.Saga.Oracle.OracleSagaTimerRecoveryRequests;

namespace Excalibur.Saga.Oracle;

internal sealed partial class OracleTimeoutFailureStore
{
    internal sealed record Profile(RecoveryTargetBinding Target, long ObjectId, string Backend);
    private sealed record FailureRow(string RowId, SagaTimeout Timeout, string? ClaimToken, string Source,
        string Cycle, bool Held, long Version, OracleTimeoutFailureFrames.Receipt? Receipt,
        SagaTimeoutDeliveryIdentity? Owned)
    {
        internal ClaimedSagaTimeout Claim(string claimToken) =>
            (Owned is null ? new ClaimedSagaTimeout(Timeout, claimToken) : new ClaimedSagaTimeout(Timeout, claimToken, Owned))
            with { SourceEpoch = Source, DeliveryEpoch = Cycle };
    }

    internal static async Task<Profile?> ReadProfileAsync(OracleConnection connection, OracleTransaction? transaction,
        RecoveryTargetBinding target, CancellationToken cancellationToken)
    {
        const string shapeSql = """
            SELECT COUNT(*),NVL(SUM(CASE
              WHEN COLUMN_NAME IN('FAILURESOURCEEPOCH','FAILUREDELIVERYEPOCH') AND DATA_TYPE='RAW' AND DATA_LENGTH=16 AND NULLABLE='N' THEN 1
              WHEN COLUMN_NAME='FAILUREHELD' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=1 AND DATA_SCALE=0 AND NULLABLE='N' THEN 1
              WHEN COLUMN_NAME='FAILUREVERSION' AND DATA_TYPE='NUMBER' AND DATA_PRECISION=19 AND DATA_SCALE=0 AND NULLABLE='N' THEN 1
              WHEN COLUMN_NAME='FAILURERECEIPT' AND DATA_TYPE='BLOB' THEN 1 ELSE 0 END),0)
            FROM ALL_TAB_COLUMNS WHERE OWNER=:Owner AND TABLE_NAME=:TableName
              AND COLUMN_NAME IN('FAILURESOURCEEPOCH','FAILUREDELIVERYEPOCH','FAILUREHELD','FAILUREVERSION','FAILURERECEIPT')
            """;
        var shape = (await ReadRecoveryAsync(connection, transaction, shapeSql,
            [Text("Owner", target.Owner, 128), Text("TableName", target.Table, 128)],
            reader => (Count: reader.GetInt32(0), Valid: reader.GetInt32(1)), cancellationToken).ConfigureAwait(false)).Single();
        if (shape.Count == 0) { return null; }
        if (shape.Count != 5 || shape.Valid != 5)
        { throw new InvalidOperationException($"Oracle timeout hold schema is incomplete/incompatible on {target.Payload}; apply script04 to this actual target before hosted delivery."); }
        const string baseShapeSql = """
            SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER=:Owner AND TABLE_NAME=:TableName AND (
              (COLUMN_NAME IN('TIMEOUTID','SAGAID') AND DATA_TYPE='VARCHAR2' AND CHAR_LENGTH=450 AND CHAR_USED='C' AND NULLABLE='N')
              OR (COLUMN_NAME IN('SAGATYPE','TIMEOUTTYPE') AND DATA_TYPE='VARCHAR2' AND CHAR_LENGTH=512 AND CHAR_USED='C' AND NULLABLE='N')
              OR (COLUMN_NAME='TIMEOUTDATA' AND DATA_TYPE='BLOB' AND NULLABLE='Y')
              OR (COLUMN_NAME IN('DUEAT','SCHEDULEDAT') AND DATA_TYPE LIKE 'TIMESTAMP%WITH TIME ZONE' AND DATA_SCALE=7 AND NULLABLE='N')
              OR (COLUMN_NAME='CLAIMEDAT' AND DATA_TYPE LIKE 'TIMESTAMP%WITH TIME ZONE' AND DATA_SCALE=7 AND NULLABLE='Y')
              OR (COLUMN_NAME='CLAIMEDBY' AND DATA_TYPE='VARCHAR2' AND CHAR_LENGTH=200 AND CHAR_USED='C' AND NULLABLE='Y')
              OR (COLUMN_NAME='TENANTID' AND DATA_TYPE='VARCHAR2' AND CHAR_LENGTH=64 AND CHAR_USED='C' AND NULLABLE='N'))
            """;
        var baseShape = (await ReadRecoveryAsync(connection, transaction, baseShapeSql,
            [Text("Owner", target.Owner, 128), Text("TableName", target.Table, 128)], reader => reader.GetInt32(0), cancellationToken).ConfigureAwait(false)).Single();
        if (baseShape != 10)
        { throw new InvalidOperationException($"Oracle timeout hold profile requires the shipped base payload/tenant/timestamp shape on {target.Payload} before hosted delivery."); }
        var defaults = (await connection.ResolveAsync(new FailureDefaultsRequest(
            target, transaction, cancellationToken)).ConfigureAwait(false))
            .ToDictionary(value => value.Name, value => new string(value.Default.Where(static character => !char.IsWhiteSpace(character) && character is not ('(' or ')')).ToArray()).ToUpperInvariant(), StringComparer.Ordinal);
        if (defaults.Count != 4 || defaults["FAILURESOURCEEPOCH"] != "SYS_GUID" || defaults["FAILUREDELIVERYEPOCH"] != "SYS_GUID"
            || defaults["FAILUREHELD"] != "0" || defaults["FAILUREVERSION"] != "0")
        { throw new InvalidOperationException($"Oracle timeout hold defaults on {target.Payload} are unusable; apply script04 before hosted delivery."); }
        var identity = (await ReadRecoveryAsync(connection, transaction,
            "SELECT OBJECT_ID,SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','CON_NAME') FROM ALL_OBJECTS WHERE OWNER=:Owner AND OBJECT_NAME=:TableName AND OBJECT_TYPE='TABLE'",
            [Text("Owner", target.Owner, 128), Text("TableName", target.Table, 128)],
            reader => (Object: checked((long)reader.GetDecimal(0)), Database: reader.GetString(1), Container: reader.GetString(2)), cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        if (identity.Object == 0)
        { throw new InvalidOperationException("Oracle timeout hold storage requires the configured direct physical table, not a view/synonym."); }
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Unicode, leaveOpen: true))
        {
            foreach (var field in new[] { connection.DataSource, identity.Database, identity.Container, target.Owner, target.Table })
            {
                writer.Write(field.Length);
                foreach (var character in field) { writer.Write((ushort)character); }
            }
            writer.Write(identity.Object);
        }
        return new(target, identity.Object, Convert.ToHexString(SHA256.HashData(stream.ToArray())));
    }

    // DATA_DEFAULT is LONG. Fetch it completely before projection; the dictionary view has no
    // selected primary key/ROWID for a later deferred LONG read.
    private sealed class FailureDefaultsRequest : DataRequestBase<IDbConnection, IEnumerable<(string Name, string Default)>>
    {
        internal FailureDefaultsRequest(RecoveryTargetBinding target, OracleTransaction? transaction, CancellationToken cancellationToken)
        {
            const string sql = "SELECT COLUMN_NAME,DATA_DEFAULT FROM ALL_TAB_COLUMNS WHERE OWNER=:Owner AND TABLE_NAME=:TableName AND COLUMN_NAME IN('FAILURESOURCEEPOCH','FAILUREDELIVERYEPOCH','FAILUREHELD','FAILUREVERSION')";
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: cancellationToken);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
            {
                BindRecovery(command, [Text("Owner", target.Owner, 128), Text("TableName", target.Table, 128)]);
                ((OracleCommand)command).InitialLONGFetchSize = -1;
            }, reader => (reader.GetString(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        }
    }

    private static string RowProjection(bool owned) =>
        "ROWIDTOCHAR(ROWID),TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId,ClaimedBy,"
        + "FailureSourceEpoch,FailureDeliveryEpoch,FailureHeld,FailureVersion,FailureReceipt,"
        + (owned ? "OwnedIncarnationId,OwnedGeneration,OwnedStateType"
            : "CAST(NULL AS RAW(16)),CAST(NULL AS NUMBER),CAST(NULL AS NVARCHAR2(500))");

    private static async Task<FailureRow?> ReadRowAsync(OracleConnection connection, OracleTransaction transaction,
        Profile profile, string tenant, string sagaId, string timeoutId, bool owned, CancellationToken cancellationToken) =>
        (await ReadRecoveryAsync(connection, transaction,
            $"SELECT {RowProjection(owned)} FROM {profile.Target.Payload} WHERE TimeoutId=:Id AND SagaId=:Saga AND TenantId=:Tenant AND LENGTH2(TimeoutId)=LENGTH2(:Id) AND LENGTH2(SagaId)=LENGTH2(:Saga) AND LENGTH2(TenantId)=LENGTH2(:Tenant) AND NLSSORT(TimeoutId,'NLS_SORT=BINARY')=NLSSORT(:Id,'NLS_SORT=BINARY') AND NLSSORT(SagaId,'NLS_SORT=BINARY')=NLSSORT(:Saga,'NLS_SORT=BINARY') AND NLSSORT(TenantId,'NLS_SORT=BINARY')=NLSSORT(:Tenant,'NLS_SORT=BINARY') FOR UPDATE",
            [Text("Id", timeoutId, 450), Text("Saga", sagaId, 450), Text("Tenant", tenant, 64)], ProjectRow, cancellationToken).ConfigureAwait(false))
        .Where(row => string.Equals(row.Timeout.TimeoutId, timeoutId, StringComparison.Ordinal))
        .Select(row => BindSource(profile, row)).SingleOrDefault();

    private static FailureRow ProjectRow(DbDataReader reader)
    {
        if (reader is not OracleDataReader oracle) { throw new InvalidOperationException("Selected Oracle reader required."); }
        var timeout = new SagaTimeout(reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
            Blob(oracle, 5), Offset(oracle, 6), Offset(oracle, 7)) { TenantId = reader.GetString(8) };
        var source = (byte[])reader.GetValue(10); var cycle = (byte[])reader.GetValue(11);
        if (source.Length != 16 || cycle.Length != 16) { throw new InvalidDataException("Oracle timeout claim epochs are incomplete."); }
        var heldNumber = reader.GetDecimal(12); var version = checked((long)reader.GetDecimal(13));
        if (heldNumber is not (0 or 1) || version < 0) { throw new InvalidDataException("Invalid Oracle timeout hold state."); }
        var frame = Blob(oracle, 14);
        var receipt = frame is null ? null : OracleTimeoutFailureFrames.DecodeReceipt(frame);
        if (heldNumber == 1 && receipt is null) { throw new InvalidDataException("Held Oracle timeout has no receipt."); }
        SagaTimeoutDeliveryIdentity? delivery = null;
        if (!reader.IsDBNull(15) || !reader.IsDBNull(16) || !reader.IsDBNull(17))
        {
            if (reader.IsDBNull(15) || reader.IsDBNull(16) || reader.IsDBNull(17) || !Guid.TryParse(timeout.SagaId, out var sagaId))
            { throw new InvalidDataException("Oracle timeout owned identity is incomplete."); }
            delivery = new(new SagaScopeIdentity(timeout.TenantId, reader.GetString(17), sagaId), timeout.TimeoutId,
                new Guid((byte[])reader.GetValue(15)), checked((long)reader.GetDecimal(16)));
        }
        return new(reader.GetString(0), timeout, reader.IsDBNull(9) ? null : reader.GetString(9), Convert.ToHexString(source),
            Convert.ToHexString(cycle), heldNumber == 1, version, receipt, delivery);
    }
    private static byte[]? Blob(OracleDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) { return null; }
        using var blob = reader.GetOracleBlob(ordinal); return blob.Value;
    }
    private static DateTimeOffset Offset(OracleDataReader reader, int ordinal)
    {
        var stamp = reader.GetOracleTimeStampTZ(ordinal);
        return new(DateTime.SpecifyKind(stamp.Value, DateTimeKind.Unspecified), stamp.GetTimeZoneOffset());
    }
    private static bool SameTarget(Profile profile, OracleTimeoutFailureFrames.Binding binding) =>
        profile.Target.Owner == binding.Owner && profile.Target.Table == binding.Table
        && profile.ObjectId == binding.ObjectId && HasSourceBinding(profile, binding.Source, binding.Tenant, binding.SagaId, binding.TimeoutId);
    private static FailureRow BindSource(Profile profile, FailureRow row) => row with
    { Source = "ORATF1:" + profile.Backend + ":" + AddressHash(row.Timeout.TenantId, row.Timeout.SagaId, row.Timeout.TimeoutId) + ":" + row.Source };
    private static bool HasSourceBinding(Profile profile, string source, string tenant, string sagaId, string timeoutId)
    {
        var parts = source.Split(':');
        return parts.Length == 4 && parts[0] == "ORATF1" && parts[1] == profile.Backend
            && parts[2] == AddressHash(tenant, sagaId, timeoutId) && IsRawEpoch(parts[3]);
    }
    private static string AddressHash(string tenant, string sagaId, string timeoutId)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Unicode, leaveOpen: true))
        {
            foreach (var field in new[] { tenant, sagaId, timeoutId })
            { writer.Write(field.Length); foreach (var character in field) { writer.Write((ushort)character); } }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static bool IsRawEpoch(string? value) => value is { Length: 32 }
        && value.All(static character => character is (>= '0' and <= '9') or (>= 'A' and <= 'F'))
        && value.Any(static character => character != '0');
    private static byte[] RawSource(string source)
    {
        var parts = source.Split(':');
        if (parts.Length != 4 || parts[0] != "ORATF1" || parts[3].Length != 32)
        { throw new ArgumentException("Oracle source epoch lacks selected-backend/address binding.", nameof(source)); }
        return Convert.FromHexString(parts[3]);
    }
    private static bool SameSnapshot(FailureRow row, ClaimedSagaTimeout claim) =>
        row.Timeout.TimeoutId == claim.Timeout.TimeoutId && row.Timeout.SagaId == claim.Timeout.SagaId
        && row.Timeout.TenantId == claim.Timeout.TenantId && row.Source == claim.SourceEpoch
        && row.Timeout.SagaType == claim.Timeout.SagaType && row.Timeout.TimeoutType == claim.Timeout.TimeoutType
        && row.Timeout.DueAt.EqualsExact(claim.Timeout.DueAt) && row.Timeout.ScheduledAt.EqualsExact(claim.Timeout.ScheduledAt)
        && EqualOwned(row.Owned, claim.OwnedDelivery)
        && ((row.Timeout.TimeoutData is null && claim.Timeout.TimeoutData is null)
            || (row.Timeout.TimeoutData is not null && claim.Timeout.TimeoutData is not null
                && row.Timeout.TimeoutData.AsSpan().SequenceEqual(claim.Timeout.TimeoutData)));
    private static bool EqualOwned(SagaTimeoutDeliveryIdentity? left, SagaTimeoutDeliveryIdentity? right) =>
        left is null ? right is null : right is not null && left.TimeoutId == right.TimeoutId
        && left.IncarnationId == right.IncarnationId && left.Generation == right.Generation
        && left.Scope.TenantId == right.Scope.TenantId && left.Scope.StateType == right.Scope.StateType && left.Scope.SagaId == right.Scope.SagaId;
}
