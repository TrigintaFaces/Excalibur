// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Orchestration;

/// <summary>Frames only original ordinary timer primitives; no runtime object graph or reflection.</summary>
internal static class SagaTimerRecoveryCodec
{
    // Preserve existing provider payload policies; add no invented global size default.
    // This is the CLR's supported owned-byte-array bound plus strict frame length/count validation.
    internal static int MaximumFrameBytes => Array.MaxLength;

    internal static byte[] Encode(IReadOnlyList<SagaTimerIntent> intents)
    {
        ArgumentNullException.ThrowIfNull(intents);
        if (intents.Count == 0 || intents.Count > MaximumFrameBytes / 5) { throw new InvalidDataException("A timer recovery batch cannot be empty."); }
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(1); writer.Write(intents.Count);
        foreach (var intent in intents)
        {
            writer.Write((byte)intent.Action); Text(writer, intent.TimeoutId);
            switch (intent.Action)
            {
                case SagaTimerAction.Schedule:
                    var timeout = intent.Timeout ?? throw new InvalidDataException("A schedule has no original envelope.");
                    Text(writer, timeout.SagaType); Text(writer, timeout.TimeoutType);
                    var data = timeout.TimeoutData;
                    if (data is not null && data.Length > MaximumFrameBytes) { throw new InvalidDataException("Timer recovery payload exceeds its bound."); }
                    writer.Write(data?.Length ?? -1);
                    if (data is not null) { writer.Write(data); }
                    Date(writer, timeout.DueAt); Date(writer, timeout.ScheduledAt);
                    break;
                case SagaTimerAction.Cancel:
                case SagaTimerAction.CancelAll:
                    if (intent.Timeout is not null) { throw new InvalidDataException("A cancellation cannot carry schedule data."); }
                    break;
                default: throw new InvalidDataException("Unknown timer recovery action.");
            }
            if (stream.Length > MaximumFrameBytes) { throw new InvalidDataException("Timer recovery frame exceeds its configured bound."); }
        }
        writer.Flush();
        var bytes = stream.ToArray();
        // Validate the same framing before it can be saved with state.
        _ = Decode(bytes, "validation-scope");
        return bytes;
    }

    internal static SagaTimerIntent[] Decode(SagaTimerRecoveryJournal journal) =>
        Decode(journal.EncodedIntents, journal.Scope.SagaId.ToString());

    private static SagaTimerIntent[] Decode(byte[] bytes, string sagaId)
    {
        if (bytes.Length < 8 || bytes.Length > MaximumFrameBytes) { throw new InvalidDataException("Invalid timer recovery frame size."); }
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadInt32() != 1) { throw new InvalidDataException("Unknown timer recovery frame version."); }
        var count = reader.ReadInt32();
        if (count < 1 || count > (bytes.Length - 8) / 5) { throw new InvalidDataException("Invalid timer recovery intent count."); }
        var intents = new SagaTimerIntent[count];
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            var action = (SagaTimerAction)reader.ReadByte();
            var id = Text(reader, 450);
            if (action == SagaTimerAction.CancelAll)
            {
                if (id.Length != 0) { throw new InvalidDataException("CancelAll has no individual ID."); }
                intents[ordinal] = new(action, id, null); continue;
            }
            if (string.IsNullOrWhiteSpace(id)) { throw new InvalidDataException("A timer ID is required."); }
            if (action == SagaTimerAction.Cancel) { intents[ordinal] = new(action, id, null); continue; }
            if (action != SagaTimerAction.Schedule) { throw new InvalidDataException("Unknown timer recovery action."); }
            var sagaType = Text(reader, 500); var timeoutType = Text(reader, 500);
            if (string.IsNullOrWhiteSpace(sagaType) || string.IsNullOrWhiteSpace(timeoutType))
            { throw new InvalidDataException("Original timer routing types are required."); }
            var length = reader.ReadInt32();
            if (length < -1 || length > stream.Length - stream.Position) { throw new InvalidDataException("Invalid timer payload length."); }
            var data = length < 0 ? null : reader.ReadBytes(length);
            var due = Date(reader); var scheduled = Date(reader);
            intents[ordinal] = new(action, id, new SagaTimeout(id, sagaId, sagaType, timeoutType, data, due, scheduled));
        }
        if (stream.Position != stream.Length) { throw new InvalidDataException("Timer recovery frame has trailing data."); }
        return intents;
    }

    private static void Text(BinaryWriter writer, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 500) { throw new InvalidDataException("Timer recovery text exceeds its schema bound."); }
        writer.Write(value.Length);
        foreach (var character in value) { writer.Write((ushort)character); }
    }
    private static string Text(BinaryReader reader, int maximum)
    {
        var length = reader.ReadInt32();
        if (length < 0 || length > maximum || length > (reader.BaseStream.Length - reader.BaseStream.Position) / 2)
        { throw new InvalidDataException("Invalid timer text length."); }
        var characters = new char[length];
        for (var i = 0; i < characters.Length; i++) { characters[i] = (char)reader.ReadUInt16(); }
        return new string(characters);
    }
    private static void Date(BinaryWriter writer, DateTimeOffset value)
    { writer.Write(value.Ticks); writer.Write(checked((short)value.Offset.TotalMinutes)); }
    private static DateTimeOffset Date(BinaryReader reader) => new(reader.ReadInt64(), TimeSpan.FromMinutes(reader.ReadInt16()));
}
