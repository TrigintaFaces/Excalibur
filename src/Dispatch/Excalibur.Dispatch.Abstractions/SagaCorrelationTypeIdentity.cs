// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Globalization;
using System.Text;

namespace Excalibur.Dispatch;

// Logical identity intentionally excludes assembly versions, strong-name keys and load contexts.
// Only static type metadata is read: no member-name lookup, activation or runtime code generation.
internal static class SagaCorrelationTypeIdentity
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static string Create(Type type)
    {
        var identity = new StringBuilder("typed/v1:");
        Append(identity, type);
        return identity.ToString();
    }
    private static void Append(StringBuilder output, Type type)
    {
        if (type.IsArray)
        {
            output.Append('A').Append(type.GetArrayRank().ToString(CultureInfo.InvariantCulture))
                .Append(type.IsSZArray ? 'S' : 'M');
            Append(output, type.GetElementType() ?? throw new ArgumentException("Array element type is unavailable.", nameof(type)));
            return;
        }
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        var name = definition.FullName ?? throw new ArgumentException("A closed named type is required.", nameof(type));
        var assembly = definition.Assembly.GetName().Name ?? throw new ArgumentException("Assembly simple name is unavailable.", nameof(type));
        output.Append('N');
        AppendPart(output, assembly); AppendPart(output, name);
        var arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
        output.Append(arguments.Length.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var argument in arguments) { Append(output, argument); }
    }
    private static void AppendPart(StringBuilder output, string value) =>
        output.Append(StrictUtf8.GetByteCount(value).ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
}
