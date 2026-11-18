// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Saga.Oracle;

// Closed primitive frames, not a serializer/registry or an authorization credential.
internal static class OracleTimeoutFailureFrames
{
    internal sealed record Receipt(Guid Operation, string OriginalClaim, string OperationClaim, string Cycle,
        Guid HoldToken, MessageFailureKind Kind, string Reason, Guid? ResumeCommand, Guid? ResumeExpected);
    internal sealed record Binding(string Owner, string Table, long ObjectId, string Tenant,
        string SagaId, string TimeoutId, string Source, string Cycle, long Version, Guid HoldToken);
    internal sealed record Cursor(string Owner, string Table, long ObjectId,
        string Backend, string Tenant, string? SagaId, string LastId);

    internal static byte[] EncodeReceipt(Receipt receipt) => Write(1, writer =>
    {
        writer.Write(receipt.Operation.ToByteArray()); Text(writer, receipt.OriginalClaim); Text(writer, receipt.OperationClaim);
        Text(writer, receipt.Cycle); writer.Write(receipt.HoldToken.ToByteArray()); writer.Write((int)receipt.Kind); Text(writer, receipt.Reason);
        writer.Write((receipt.ResumeCommand ?? Guid.Empty).ToByteArray()); writer.Write((receipt.ResumeExpected ?? Guid.Empty).ToByteArray());
    });
    internal static Receipt DecodeReceipt(byte[] bytes) => Read<Receipt>(bytes, 1, reader =>
    {
        var operation = GuidValue(reader); var original = Text(reader); var current = Text(reader); var cycle = Text(reader);
        var token = GuidValue(reader); var kind = (MessageFailureKind)reader.ReadInt32(); var reason = Text(reader);
        var resume = GuidValue(reader); var expected = GuidValue(reader);
        if (operation == Guid.Empty || token == Guid.Empty || reason.Length > 1024
            || kind is not (MessageFailureKind.Permanent or MessageFailureKind.Poison or MessageFailureKind.Unknown))
        { throw new InvalidDataException("Invalid retained Oracle timeout hold receipt."); }
        return new(operation, original, current, cycle, token, kind, reason,
            resume == Guid.Empty ? null : resume, expected == Guid.Empty ? null : expected);
    });
    internal static string EncodeBinding(Binding value) => Convert.ToBase64String(Write(2, writer =>
    {
        Target(writer, value.Owner, value.Table, value.ObjectId);
        Text(writer, value.Tenant); Text(writer, value.SagaId); Text(writer, value.TimeoutId);
        Text(writer, value.Source); Text(writer, value.Cycle); writer.Write(value.Version); writer.Write(value.HoldToken.ToByteArray());
    }));
    internal static Binding DecodeBinding(string value) => Read<Binding>(Convert.FromBase64String(value), 2, reader =>
    {
        var target = Target(reader); return new(target.Owner, target.Table, target.ObjectId,
            Text(reader), Text(reader), Text(reader), Text(reader), Text(reader), reader.ReadInt64(), GuidValue(reader));
    });
    internal static string EncodeCursor(Cursor value) => Convert.ToBase64String(Write(3, writer =>
    {
        Target(writer, value.Owner, value.Table, value.ObjectId); Text(writer, value.Backend); Text(writer, value.Tenant);
        writer.Write(value.SagaId is not null); if (value.SagaId is not null) { Text(writer, value.SagaId); } Text(writer, value.LastId);
    }));
    internal static Cursor DecodeCursor(string value) => Read<Cursor>(Convert.FromBase64String(value), 3, reader =>
    {
        var target = Target(reader); var backend = Text(reader); var tenant = Text(reader); var saga = reader.ReadBoolean() ? Text(reader) : null;
        return new(target.Owner, target.Table, target.ObjectId, backend, tenant, saga, Text(reader));
    });
    private static byte[] Write(int kind, Action<BinaryWriter> body)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(1); writer.Write(kind); body(writer); writer.Flush(); return stream.ToArray();
    }
    private static T Read<T>(byte[] bytes, int kind, Func<BinaryReader, T> body)
    {
        if (bytes.Length > 16384) { throw new InvalidDataException("Oracle timeout hold frame is oversized."); }
        using var stream = new MemoryStream(bytes, writable: false); using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != 1 || reader.ReadInt32() != kind) { throw new InvalidDataException("Unknown Oracle timeout hold frame."); }
        var result = body(reader);
        if (stream.Position != stream.Length) { throw new InvalidDataException("Trailing Oracle timeout hold frame data."); }
        return result;
    }
    private static void Text(BinaryWriter writer, string value)
    {
        if (value.Length > 2048) { throw new ArgumentOutOfRangeException(nameof(value)); }
        writer.Write(value.Length); foreach (var character in value) { writer.Write((ushort)character); }
    }
    private static string Text(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length is < 0 or > 2048) { throw new InvalidDataException("Invalid Oracle timeout hold text length."); }
        var characters = new char[length]; for (var index = 0; index < length; index++) { characters[index] = (char)reader.ReadUInt16(); }
        return new string(characters);
    }
    private static Guid GuidValue(BinaryReader reader)
    {
        var value = reader.ReadBytes(16); if (value.Length != 16) { throw new EndOfStreamException(); } return new Guid(value);
    }
    private static void Target(BinaryWriter writer, string owner, string table, long objectId)
    { Text(writer, owner); Text(writer, table); writer.Write(objectId); }
    private static (string Owner, string Table, long ObjectId) Target(BinaryReader reader) =>
        (Text(reader), Text(reader), reader.ReadInt64());
}
