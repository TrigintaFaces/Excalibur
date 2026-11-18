// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Workflows;

/// <summary>Creates decided payload options and serializes through their explicit metadata.</summary>
internal static class WorkflowPayloadSerializer
{
    /// <summary>Composes generated framework metadata with a non-null consumer resolver.</summary>
    /// <param name="consumerPayloadResolver">The consumer's payload metadata resolver.</param>
    /// <returns>Read-only options with no implicit reflection fallback.</returns>
    internal static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver consumerPayloadResolver)
    {
        ArgumentNullException.ThrowIfNull(consumerPayloadResolver);
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                WorkflowJsonSerializerContext.Default,
                consumerPayloadResolver),
        };
        options.MakeReadOnly();
        return options;
    }

    /// <summary>Explicitly creates reflection-based payload options for the legacy JIT registration.</summary>
    /// <returns>Read-only options with the reflection resolver explicitly installed.</returns>
    [RequiresUnreferencedCode("Workflow payload reflection can access trimmed members. Use AddWorkflows with a source-generated IJsonTypeInfoResolver.")]
    [RequiresDynamicCode("Workflow payload reflection can require runtime code generation. Use AddWorkflows with a source-generated IJsonTypeInfoResolver.")]
    internal static JsonSerializerOptions CreateReflectionOptions()
    {
        // This is a runtime NativeAOT refusal, not a claimed trimming guard. The factory remains annotated.
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException(
                "The reflection-based workflow payload registration is unavailable under Native AOT. "
                + "Use AddWorkflows with a source-generated IJsonTypeInfoResolver covering all payload types.");
        }

        return CreateOptions(new DefaultJsonTypeInfoResolver());
    }

    /// <summary>Serializes a non-null value using the already-decided metadata, preserving null payloads.</summary>
    /// <param name="value">The payload.</param>
    /// <param name="options">The prepared options.</param>
    /// <returns>The serialized payload, or null.</returns>
    internal static string? Serialize(object? value, JsonSerializerOptions options) =>
        value is null ? null : JsonSerializer.Serialize(value, options.GetTypeInfo(value.GetType()));

    /// <summary>Deserializes a non-null journal payload through explicit metadata.</summary>
    /// <typeparam name="TResult">The requested result type.</typeparam>
    /// <param name="json">The recorded payload, or null.</param>
    /// <param name="options">The prepared options.</param>
    /// <returns>The result, or default for a null recorded payload.</returns>
    internal static TResult Deserialize<TResult>(string? json, JsonSerializerOptions options) =>
        json is null ? default! : (TResult)JsonSerializer.Deserialize(json, options.GetTypeInfo(typeof(TResult)))!;
}
