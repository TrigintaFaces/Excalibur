// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

/// <summary>
/// The CloudEvents payload-serialization seam: two factories, one reflection-free and one annotated.
/// <para>
/// They are separate methods rather than one factory taking a nullable resolver, and these arms bind
/// the consequences of that. A nullable parameter would select between two materially different
/// contracts on an argument's nullness, and — measured — would force the trimming annotation onto both
/// callers or neither, because no runtime guard can establish a <i>trimming</i> precondition:
/// trimming and dynamic-code support are orthogonal, and a trimmed but JIT-capable host has dynamic
/// code available with the reflected-over members removed anyway.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.CloudEvents")]
public sealed class CloudEventPayloadSerializationShould
{
    [Fact]
    public void ComposeTheConsumerResolverAfterTheFrameworkEnvelopeContextAndFreezeTheResult()
    {
        var options = CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default);

        options.TypeInfoResolver.ShouldNotBeNull();
        options.IsReadOnly.ShouldBeTrue(
            "a resolver chain that can still be mutated after first use is a different contract from "
            + "one that cannot");

        // LIVENESS, and the half that would be forgotten: the composed chain must actually resolve
        // BOTH halves. A chain that resolves neither is as 'safe' as one that resolves both, and an
        // arm asserting only non-null cannot tell them apart.
        options.TypeInfoResolver!
            .GetTypeInfo(typeof(CloudEvent), options)
            .ShouldNotBeNull("the framework's envelope metadata must be reachable through the chain");

        options.TypeInfoResolver!
            .GetTypeInfo(typeof(ConformancePayload), options)
            .ShouldNotBeNull("the consumer's payload metadata must be reachable through the chain");
    }

    [Fact]
    public void CoverTheDecodedPayloadShapeSoAForwardedEventCanBeReEncoded()
    {
        // A decoded CloudEvent's data is a JsonElement. With a resolver set there is no reflection
        // fallback, so if the framework context does not declare JsonElement then receiving an event
        // and republishing it fails -- on the trim-safe configuration specifically.
        var options = CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default);

        options.TypeInfoResolver!
            .GetTypeInfo(typeof(JsonElement), options)
            .ShouldNotBeNull("a forwarded payload is a JsonElement, so the chain must resolve one");
    }

    [Fact]
    public void RefuseANullResolverRatherThanSilentlyFallingBackToReflection()
    {
        // The seam's whole shape depends on this. If a null resolver quietly selected the reflection
        // path, the annotation could not live on the unsafe contract -- the caller who opted into
        // reflection and the caller who did not would be indistinguishable at the call site.
        _ = Should.Throw<ArgumentNullException>(() =>
            CloudEventPayloadSerialization.CreateOptions(payloadTypeInfoResolver: null!));
    }

    [Fact]
    public void ResolveAnUndeclaredTypeOnTheReflectionPath()
    {
        // The majority configuration: a host on a full runtime with no source-generated resolver.
        // ConformanceUndeclared is deliberately absent from every JsonSerializerContext in this
        // assembly, so this arm fails if the reflection path stops being reflective -- which is what
        // makes it a check rather than a restatement.
        var options = CloudEventPayloadSerialization.CreateReflectionOptions();

        options.IsReadOnly.ShouldBeTrue();
        options.TypeInfoResolver!
            .GetTypeInfo(typeof(ConformanceUndeclared), options)
            .ShouldNotBeNull("the reflection path must resolve a type no generated context declares");

        // And the envelope still comes from generated metadata even here.
        options.TypeInfoResolver!
            .GetTypeInfo(typeof(CloudEvent), options)
            .ShouldNotBeNull();
    }

    [Fact]
    public void SerializeAConsumerPayloadThroughTheComposedChain()
    {
        var options = CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default);

        var json = JsonSerializer.Serialize(
            new ConformancePayload { Name = "alpha", Count = 22 },
            options.GetTypeInfo(typeof(ConformancePayload)));

        json.ShouldContain("alpha");
        json.ShouldContain("22");
    }
}

/// <summary>A consumer payload type, standing in for whatever a host publishes as CloudEvent data.</summary>
internal sealed class ConformancePayload
{
    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>
/// Deliberately NOT declared in any <see cref="JsonSerializerContext"/>, so an arm asserting that the
/// reflection path resolves it cannot pass by accident.
/// </summary>
internal sealed class ConformanceUndeclared
{
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// A consumer-supplied source-generated resolver. At file scope because the generator requires every
/// containing type to be partial as well, and a nested context inside a sealed test class is not.
/// </summary>
[JsonSerializable(typeof(ConformancePayload))]
internal sealed partial class PayloadContext : JsonSerializerContext;
