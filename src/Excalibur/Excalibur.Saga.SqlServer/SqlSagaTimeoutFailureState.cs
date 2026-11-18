// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

internal static class SqlSagaTimeoutFailureState
{
    internal sealed record Metadata(int Target, bool Available, bool OwnedColumns, Guid TargetEpoch);
    internal sealed record Row(SagaTimeout Timeout, string? Claim, SagaTimeoutDeliveryIdentity? Owned,
        Guid Source, Guid Delivery, bool Held, long Version, Guid? Token, Guid? Operation,
        string? OriginalClaim, string? HoldClaim, Guid? ObservedDelivery, MessageFailureKind? Kind,
        string? Reason, Guid? ResumeCommand);
    internal sealed record Handle(string Backend, Guid Source, Guid Delivery, long Version, Guid Token,
        string Tenant, string Saga, string TimeoutId);

    internal static bool SameTimeout(SagaTimeout left, SagaTimeout right) =>
        left.TimeoutId == right.TimeoutId && left.TenantId == right.TenantId && left.SagaId == right.SagaId
        && left.SagaType == right.SagaType && left.TimeoutType == right.TimeoutType
        && left.DueAt.EqualsExact(right.DueAt) && left.ScheduledAt.EqualsExact(right.ScheduledAt)
        && (left.TimeoutData is null ? right.TimeoutData is null
            : right.TimeoutData is not null && left.TimeoutData.AsSpan().SequenceEqual(right.TimeoutData));

    internal static bool SameAuthority(SagaTimeoutDeliveryIdentity? left, SagaTimeoutDeliveryIdentity? right) =>
        left is null ? right is null : right is not null && left.TimeoutId == right.TimeoutId
            && left.Scope.TenantId == right.Scope.TenantId && left.Scope.StateType == right.Scope.StateType
            && left.Scope.SagaId == right.Scope.SagaId && left.IncarnationId == right.IncarnationId && left.Generation == right.Generation;

    internal static bool MatchesObservation(Row row, SagaTimeoutFailureObservation observation, string backend)
    {
        var original = observation.OriginalClaim;
        return SourceEpoch(backend, row.Source, row.Timeout) == original.SourceEpoch && SameTimeout(row.Timeout, original.Timeout)
            && SameAuthority(row.Owned, original.OwnedDelivery);
    }

    internal static string SourceEpoch(string backend, Guid source, SagaTimeout timeout) => backend + "/" + source.ToString("N") + "/" + Identity(timeout);
    internal static bool TrySourceEpoch(string? value, string backend, SagaTimeout timeout, out Guid source)
    {
        source = Guid.Empty;
        if (value is null) { return false; }
        var parts = value.Split('/');
        return parts.Length == 3 && parts[0] == backend && parts[2] == Identity(timeout)
            && Guid.TryParseExact(parts[1], "N", out source) && source != Guid.Empty
            && parts[1] == source.ToString("N");
    }

    private static string Identity(SagaTimeout timeout)
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.Unicode, leaveOpen: true))
        { writer.Write(timeout.TenantId!); writer.Write(timeout.SagaId); writer.Write(timeout.TimeoutId); }
        return Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
    }

    internal static string Backend(SqlConnection connection, int target, Guid targetEpoch)
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.Unicode, leaveOpen: true))
        { writer.Write(connection.DataSource); writer.Write(connection.Database); writer.Write(target); writer.Write(targetEpoch.ToByteArray()); }
        return Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
    }

    internal static SagaTimeoutHoldToken Token(string backend, Row row) => new(string.Join('|',
        "sql-timeout-hold-v1", backend, row.Source.ToString("N"), row.ObservedDelivery!.Value.ToString("N"),
        row.Version.ToString(CultureInfo.InvariantCulture), row.Token!.Value.ToString("N"),
        Part(row.Timeout.TenantId!), Part(row.Timeout.SagaId), Part(row.Timeout.TimeoutId)));

    internal static Handle? Parse(SagaTimeoutHoldToken token)
    {
        var parts = token.Value.Split('|');
        if (parts.Length != 9 || parts[0] != "sql-timeout-hold-v1"
            || !Guid.TryParseExact(parts[2], "N", out var source) || source == Guid.Empty
            || !Guid.TryParseExact(parts[3], "N", out var delivery) || delivery == Guid.Empty
            || !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1
            || !Guid.TryParseExact(parts[5], "N", out var id) || id == Guid.Empty) { return null; }
        try { return new(parts[1], source, delivery, version, id, Unpart(parts[6]), Unpart(parts[7]), Unpart(parts[8])); }
        catch (FormatException) { return null; }
    }

    internal static SagaTimeoutHoldReceipt Receipt(string backend, Row row) => new(Token(backend, row),
        row.Held ? SagaTimeoutHoldState.Held : SagaTimeoutHoldState.Resumed, row.Timeout, row.Kind!.Value, row.Reason!);
    internal static string Part(string text) => Convert.ToBase64String(Encoding.Unicode.GetBytes(text));
    internal static string Unpart(string text) => Encoding.Unicode.GetString(Convert.FromBase64String(text));
}
