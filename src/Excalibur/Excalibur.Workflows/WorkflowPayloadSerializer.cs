// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Workflows;

/// <summary>
/// Serializes activity inputs and results to and from the workflow journal's string payload fields, using a
/// serialization context composed from the framework-owned source-generated types and an optional
/// consumer-supplied payload resolver.
/// </summary>
/// <remarks>
/// When the consumer supplies a source-generated payload resolver (<see cref="WorkflowOptions.PayloadTypeInfoResolver"/>),
/// payloads round-trip without reflection under trimming and native AOT — the framework's own
/// <see cref="WorkflowJsonSerializerContext"/> is composed ahead of it via
/// <see cref="JsonTypeInfoResolver.Combine(IJsonTypeInfoResolver[])"/>. When no resolver is supplied the
/// default is reflection-based <see cref="JsonSerializer"/>, which works but is not trim/AOT-safe for unknown
/// payload types; that reflection dependency is confined to this boundary and never surfaces on the
/// consumer-facing API.
/// </remarks>
internal static class WorkflowPayloadSerializer
{
    private const string AotJustification =
        "Reflection-based serialization is only the default payload path when the consumer supplies no "
        + "source-generated resolver; a consumer that sets WorkflowOptions.PayloadTypeInfoResolver gets a "
        + "reflection-free, AOT-safe path. The suppression is confined here and never surfaces on the "
        + "consumer-facing API.";

    /// <summary>
    /// Builds the payload serializer options for the given consumer resolver. With a resolver, payload types
    /// resolve through the framework context then the consumer's source-generated context (no reflection);
    /// without one, a reflection-based default is used.
    /// </summary>
    /// <param name="consumerPayloadResolver">The consumer payload resolver, or <see langword="null"/> for the reflection default.</param>
    /// <returns>The read-only serializer options to use for payload serialization.</returns>
    internal static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver? consumerPayloadResolver) =>
        CreateOptions(consumerPayloadResolver, RuntimeFeature.IsDynamicCodeSupported);

    /// <summary>
    /// Testable overload: <paramref name="dynamicCodeSupported"/> is injected so the AOT fail-loud guard can
    /// be exercised deterministically (the ambient <see cref="RuntimeFeature.IsDynamicCodeSupported"/> cannot
    /// be flipped in-process). Production callers use the single-argument overload.
    /// </summary>
    /// <param name="consumerPayloadResolver">The consumer payload resolver, or <see langword="null"/> for the reflection default.</param>
    /// <param name="dynamicCodeSupported">Whether the runtime supports dynamic code (reflection serialization).</param>
    /// <returns>The read-only serializer options to use for payload serialization.</returns>
    internal static JsonSerializerOptions CreateOptions(
        IJsonTypeInfoResolver? consumerPayloadResolver,
        bool dynamicCodeSupported)
    {
        if (consumerPayloadResolver is null)
        {
            // No consumer resolver: reflection default. Works under JIT; NOT trim/AOT-safe for unknown
            // payload types. Under native AOT (dynamic code unsupported) reflection-based serialization
            // would otherwise fail deep in System.Text.Json with an opaque message — fail loud and
            // actionable HERE instead, pointing the consumer at the source-generated resolver seam. This
            // is the honest boundary for the blanket assembly IsAotCompatible claim: the reflection path
            // is only reachable when dynamic code IS supported.
            if (!dynamicCodeSupported)
            {
                throw new NotSupportedException(
                    "Workflow activity payload serialization requires a source-generated JSON resolver under "
                    + "native AOT. Set WorkflowOptions.PayloadTypeInfoResolver to a JsonSerializerContext that "
                    + "covers your activity input/result types; the reflection-based default is not AOT-safe.");
            }

            return new JsonSerializerOptions();
        }

        // Consumer-supplied source-gen resolver, composed after the framework-owned context: a fully
        // reflection-free, AOT-safe payload path.
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                WorkflowJsonSerializerContext.Default,
                consumerPayloadResolver),
        };
        options.MakeReadOnly();
        return options;
    }

    /// <summary>
    /// Serializes a payload to its journal string form, or <see langword="null"/> when the payload is null.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJustification)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification)]
    internal static string? Serialize(object? value, JsonSerializerOptions options) =>
        value is null ? null : JsonSerializer.Serialize(value, value.GetType(), options);

    /// <summary>
    /// Deserializes a journal string payload back to the expected type, or <see langword="default"/> when
    /// the stored payload is null.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJustification)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification)]
    internal static TResult Deserialize<TResult>(string? json, JsonSerializerOptions options) =>
        json is null ? default! : JsonSerializer.Deserialize<TResult>(json, options)!;
}
