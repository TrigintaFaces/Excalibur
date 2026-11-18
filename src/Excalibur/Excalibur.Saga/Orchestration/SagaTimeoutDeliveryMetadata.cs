// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Orchestration;

/// <summary>Encodes original timeout authority using bounded explicitly propagated primitive values.</summary>
internal static class SagaTimeoutDeliveryMetadata
{
    internal const string VersionKey = "excalibur.saga.timeout.version";
    internal const string StateFirstKey = "excalibur.saga.timeout.state.0";
    internal const string StateLastKey = "excalibur.saga.timeout.state.1";
    internal const string IdFirstKey = "excalibur.saga.timeout.id.0";
    internal const string IdLastKey = "excalibur.saga.timeout.id.1";
    internal const string SagaKey = "excalibur.saga.timeout.saga";
    internal const string IncarnationKey = "excalibur.saga.timeout.incarnation";
    internal const string GenerationKey = "excalibur.saga.timeout.generation";

    private const int PartLength = 1024;
    private static readonly string[] Keys =
        [VersionKey, StateFirstKey, StateLastKey, IdFirstKey, IdLastKey, SagaKey, IncarnationKey, GenerationKey];

    /// <summary>Consumes inbound authority only on a newly owned emitted child context.</summary>
    internal static void RemoveFromOwnedChild(IMessageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var key in Keys)
        {
            context.RemovePropagatedItem(key);
        }
    }

    internal static bool HasAny(IMessageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var values = context.GetPropagatedItems();
        foreach (var key in Keys)
        {
            if (values.ContainsKey(key)) { return true; }
        }

        return false;
    }

    internal static void Write(IMessageContext context, SagaTimeoutDeliveryIdentity delivery)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(delivery);
        if (HasAny(context))
        {
            throw new InvalidOperationException("Original timeout authority cannot be overwritten.");
        }

        var state = EncodeExact(delivery.Scope.StateType);
        var id = EncodeExact(delivery.TimeoutId);
        context.SetPropagatedItem(VersionKey, "1");
        WriteParts(context, StateFirstKey, StateLastKey, state);
        WriteParts(context, IdFirstKey, IdLastKey, id);
        context.SetPropagatedItem(SagaKey, delivery.Scope.SagaId.ToString("N"));
        context.SetPropagatedItem(IncarnationKey, delivery.IncarnationId.ToString("N"));
        context.SetPropagatedItem(GenerationKey, delivery.Generation.ToString(CultureInfo.InvariantCulture));
    }

    internal static SagaTimeoutDeliveryIdentity? Read(IMessageContext context, string authoritativeTenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!HasAny(context)) { return null; }
        var values = context.GetPropagatedItems();
        if (!string.Equals(Required(values, VersionKey), "1", StringComparison.Ordinal)) { throw Malformed(); }
        var state = ReadParts(values, StateFirstKey, StateLastKey, 500);
        var id = ReadParts(values, IdFirstKey, IdLastKey, 450);
        var saga = ReadGuid(Required(values, SagaKey));
        var incarnation = ReadGuid(Required(values, IncarnationKey));
        var generationText = Required(values, GenerationKey);
        if (!long.TryParse(generationText, NumberStyles.None, CultureInfo.InvariantCulture, out var generation)
            || generation < 0 || !string.Equals(generation.ToString(CultureInfo.InvariantCulture), generationText, StringComparison.Ordinal))
        {
            throw Malformed();
        }

        return new SagaTimeoutDeliveryIdentity(new SagaScopeIdentity(authoritativeTenant, state, saga), id, incarnation, generation);
    }

    private static void WriteParts(IMessageContext context, string firstKey, string lastKey, string value)
    {
        context.SetPropagatedItem(firstKey, value[..Math.Min(PartLength, value.Length)]);
        // Always present and nonempty, including on transports that reject empty attribute values.
        context.SetPropagatedItem(lastKey, value.Length > PartLength ? value[PartLength..] : "-");
    }

    private static string ReadParts(IReadOnlyDictionary<string, string> values, string firstKey, string lastKey, int maxCharacters)
    {
        var first = Required(values, firstKey);
        var last = Required(values, lastKey);
        if (first.Length is 0 or > PartLength || last.Length is 0 or > PartLength) { throw Malformed(); }
        var encoded = string.Equals(last, "-", StringComparison.Ordinal) ? first : first + last;
        if (encoded.Length > (checked(maxCharacters * 2) + 2) / 3 * 4) { throw Malformed(); }
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException error) { throw new InvalidOperationException("Malformed original timeout authority.", error); }
        if (bytes.Length == 0 || (bytes.Length % 2) != 0 || bytes.Length > maxCharacters * 2) { throw Malformed(); }
        var characters = new char[bytes.Length / 2];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = (char)(bytes[index * 2] | (bytes[(index * 2) + 1] << 8));
        }

        var decoded = new string(characters);
        var canonical = EncodeExact(decoded);
        if (!string.Equals(canonical, encoded, StringComparison.Ordinal)
            || !string.Equals(first, canonical[..Math.Min(PartLength, canonical.Length)], StringComparison.Ordinal)
            || !string.Equals(last, canonical.Length > PartLength ? canonical[PartLength..] : "-", StringComparison.Ordinal))
        {
            throw Malformed();
        }

        return decoded;
    }

    private static string EncodeExact(string value)
    {
        var bytes = new byte[checked(value.Length * 2)];
        for (var index = 0; index < value.Length; index++)
        {
            bytes[index * 2] = (byte)value[index];
            bytes[(index * 2) + 1] = (byte)(value[index] >> 8);
        }

        return Convert.ToBase64String(bytes);
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && value is not null ? value : throw Malformed();

    private static Guid ReadGuid(string value)
    {
        if (!Guid.TryParseExact(value, "N", out var result) || result == Guid.Empty
            || !string.Equals(result.ToString("N"), value, StringComparison.Ordinal)) { throw Malformed(); }
        return result;
    }

    private static InvalidOperationException Malformed() => new("Malformed or incomplete original timeout authority.");
}
