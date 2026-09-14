// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.Grpc;
using Excalibur.Dispatch.Transport.IbmMq;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.Mqtt;
using Excalibur.Dispatch.Transport.Pulsar;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Binds WHICH transports carry the shared structured CloudEvents encoder, as distinct from whether the
/// encoder works.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are TRIPWIRE arms, not verification arms.</b> They assert a decision that was ruled, not a
/// fact that was measured. A green here means the ruled scope still holds; it is NOT evidence that the
/// scope is correct, and must never be cited as such. If the ruling is wrong these arms will faithfully
/// defend the error.
/// </para>
/// <para>
/// <b>Why the scope needs binding at all.</b> The encoder is a decorator, so it is the OUTERMOST stage of
/// whichever sender it wraps: it rewrites body and content type before the inner transport sees anything.
/// On a transport that already has its own conformant binary binding, adding it would silently override
/// that binding. Nothing else in the suite observes which transports carry it, so a registration added
/// later is invisible to every other arm.
/// </para>
/// <para>
/// <b>Why two instruments.</b> The resolution arm proves the wrapping actually happens, but it can only
/// cover a transport whose client is cheap to construct without infrastructure. The metadata arm covers
/// the whole population in both directions, but it observes a reference rather than a call, so it would
/// not notice an encoder referenced and never applied. Each covers the other's blind spot; neither is
/// sufficient alone, which is why both are here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventEncodingScopeShould
{
    private const string EncoderEntryPointType = "CloudEventEncodingSenderExtensions";

    private const string TransportAssemblyPrefix = "Excalibur.Dispatch.Transport.";

    /// <summary>The transports ruled to carry the shared structured encoder.</summary>
    /// <remarks>
    /// <para>
    /// <b>IbmMq joined this set deliberately, and the reason it was ABSENT is the reason it can now
    /// join.</b> Structured mode is identified by its media type alone. This transport used to discard
    /// the content type on send outright, and on receive it substituted MQ's wire-format tag
    /// (<c>MQSTR</c>, <c>MQFMT_NONE</c>) into the MIME field -- so an encoded CloudEvent would have
    /// reached the wire carrying nothing a conformant receiver could recognise it by. The content type
    /// now travels as a message property and survives the round trip, which removes the blocker.
    /// </para>
    /// <para>
    /// <b>The override concern this tripwire raises does not apply here, and it was checked rather than
    /// assumed.</b> The shared encoder is the outermost sender stage, so a transport that ships its own
    /// protocol binding would have that binding overridden by joining. IbmMq ships none -- zero
    /// CloudEvents types in the package, against a control of seven in Kafka -- so nothing is displaced.
    /// A transport WITH its own binding must not be added here without answering that question first.
    /// </para>
    /// </remarks>
    private static readonly string[] RuledEncodingTransports =
    [
        "Excalibur.Dispatch.Transport.Grpc",
        "Excalibur.Dispatch.Transport.IbmMq",
        "Excalibur.Dispatch.Transport.Pulsar",
    ];

    /// <summary>One type per transport package, used only to force its assembly to load.</summary>
    private static readonly Type[] LoadAnchors =
    [
        typeof(AzureServiceBusOptions), typeof(AwsSqsOptions), typeof(GooglePubSubOptions),
        typeof(KafkaOptions), typeof(RabbitMqOptions), typeof(MqttOptions),
        typeof(IbmMqOptions), typeof(PulsarOptions), typeof(GrpcTransportOptions),
    ];

    /// <summary>
    /// SAFETY and LIVENESS together, over the WHOLE population: exactly the ruled transports reference the
    /// encoder, and every other transport does not.
    /// </summary>
    /// <remarks>
    /// This is the arm that fails when the shared encoder is added to a transport that already has a
    /// conformant binary binding. Both directions are asserted, so a forgotten addition AND a forgotten
    /// removal each go red.
    /// </remarks>
    [Fact]
    public void Bind_the_encoding_scope_to_exactly_the_ruled_transports()
    {
        var transports = LoadedTransportAssemblies();

        transports.Count.ShouldBeGreaterThanOrEqualTo(
            LoadAnchors.Length,
            "the population is read back from loaded assemblies rather than from the anchor list; if it is "
            + "short of the anchors then a load did not happen and this arm is silently measuring a subset "
            + "of the transports it claims to cover");

        var referencing = transports
            .Where(pair => ReferencesEncoderEntryPoint(pair.Value))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        referencing.ShouldBe(
            RuledEncodingTransports.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            "TRIPWIRE - this states a ruled policy, not a verified fact, and its green is not evidence the "
            + "policy is right. The shared structured encoder is the outermost sender stage, so a transport "
            + "carrying it has its own binding overridden. A transport entering or leaving this set is a "
            + "decision: if it is deliberate, change the ruled set here and record why. Do not relax the "
            + "assertion to make it pass.");
    }

    /// <summary>
    /// LIVENESS through the door a consumer walks through: the gRPC registration really does wrap its
    /// sender, rather than merely referencing the encoder somewhere in the package.
    /// </summary>
    /// <remarks>
    /// This is the arm the metadata arm cannot be: a reference is not a call, and only resolution shows
    /// the wrapping reached the instance a consumer receives.
    /// </remarks>
    [Fact]
    public async Task Resolve_an_encoding_sender_for_Grpc()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddGrpcTransport("scope", options => options.ServerAddress = "https://localhost:5001");

        // await using: the decorator is IAsyncDisposable, and the container refuses to dispose such a
        // singleton synchronously. A sync using here fails AFTER a successful resolve, which reads as a
        // wiring failure and is not one.
        await using var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredKeyedService<ITransportSender>("scope");

        _ = sender.ShouldBeOfType<Decorators.CloudEventEncodingTransportSender>(
            "gRPC is in the ruled encoding scope, so the sender a consumer resolves must be wrapped. An "
            + "unwrapped sender ships the caller's raw body with no CloudEvents framing - silently, and "
            + "indistinguishably from a message that never carried an event.");
    }

    private static Dictionary<string, Assembly> LoadedTransportAssemblies()
    {
        foreach (var anchor in LoadAnchors)
        {
            _ = anchor.Assembly;
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => (Name: assembly.GetName().Name ?? string.Empty, Assembly: assembly))
            .Where(entry => entry.Name.StartsWith(TransportAssemblyPrefix, StringComparison.Ordinal))
            .Where(entry => entry.Name != TransportAssemblyPrefix + "Abstractions")
            .Where(entry => !entry.Name.EndsWith(".Tests", StringComparison.Ordinal))
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Assembly, StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the assembly's type-reference table for the encoder's public entry point. Metadata rather
    /// than reflection, because a reference to a static extension class is not observable through the
    /// referencing assembly's runtime type system.
    /// </summary>
    private static bool ReferencesEncoderEntryPoint(Assembly assembly)
    {
        var location = assembly.Location;

        if (string.IsNullOrEmpty(location) || !File.Exists(location))
        {
            throw new InvalidOperationException(
                $"Cannot read metadata for '{assembly.GetName().Name}'. An unreadable assembly would make "
                + "this arm under-report the encoding scope, which is the failure it exists to catch, so "
                + "it fails loudly instead of returning false.");
        }

        using var stream = File.OpenRead(location);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        return reader.TypeReferences
            .Select(reader.GetTypeReference)
            .Any(typeReference => string.Equals(
                reader.GetString(typeReference.Name),
                EncoderEntryPointType,
                StringComparison.Ordinal));
    }
}
