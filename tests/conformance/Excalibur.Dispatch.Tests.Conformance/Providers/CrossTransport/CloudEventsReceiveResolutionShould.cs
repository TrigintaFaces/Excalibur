// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Conformance.Providers.CrossTransport;

/// <summary>
/// Binds the PRESENCE of the decoding seam on a transport's receive path: the receiver a consumer
/// resolves is the decoding decorator rather than the bare one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why resolution rather than reflection.</b> The claim "this transport can receive a CloudEvent" is
/// made by a <i>registration call site</i>, and reflection over a loaded assembly cannot observe a call
/// site — it sees declared types only. The sibling wiring suite therefore states plainly that it makes no
/// claim about receive. Resolution can see the call site's <i>effect</i>: the factory either wrapped the
/// receiver or it did not, and the resolved instance says which.
/// </para>
/// <para>
/// <b>Why this keys on the EFFECT and not on a mechanism.</b> An earlier tripwire asserted that no receive
/// surface accepted a particular bridge type as a constructor parameter. That was falsifiable when written
/// and stopped being so when the design moved: receive shipped as a decorator taking a decoder, so the
/// assertion stayed true, stayed green, and reported honestly on a question that no longer mattered. These
/// arms assert that the resolved receiver <i>is</i> the decoding decorator, which holds for any wiring
/// that routes receive through a decoder — a decorator, a bridge, or something not yet imagined.
/// </para>
/// <para>
/// <b>Necessary, and NOT sufficient — stated because the summary used to claim more.</b> The decorator's
/// presence does not say what it decodes. A registration can construct it with a binding that reads
/// nothing this transport emits, and these arms are green for that transport exactly as they are for a
/// correct one. Measured: the send-receive defect that prompted this suite changed a single ARGUMENT to
/// the decoration call, leaving the decorator in place on both sides — so this predicate was green before
/// the fix and after it, and could not have caught it. What a transport decodes is bound by feeding a
/// resolved receiver the output its own adapter produced; that exists for one transport and is named
/// there, not here.
/// </para>
/// <para>
/// <b>No infrastructure.</b> Each transport's receiver factory is deferred and takes its client from the
/// container, so a fake client is enough. Nothing here connects to a broker.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CloudEventsReceiveResolutionShould
{
    private const string TransportName = "conformance";

    /// <summary>
    /// LIVENESS. MQTT's registered receiver decodes.
    /// </summary>
    [Fact]
    public async Task Resolve_a_decoding_receiver_for_Mqtt()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddKeyedSingleton(TransportName, A.Fake<IMqttConnectionProvider>());

        _ = services.AddMqttTransport(TransportName, o =>
        {
            o.Host = "localhost";
            o.ClientId = "conformance";
            o.Topic = "conformance";
            o.RequireTls = false;
        });

        await AssertDecodesAsync(services, "Mqtt");
    }

    /// <summary>
    /// LIVENESS. Kafka's registered receiver decodes.
    /// </summary>
    [Fact]
    public async Task Resolve_a_decoding_receiver_for_Kafka()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(A.Fake<IConsumer<string, byte[]>>());

        // The builder's defaults are sufficient: BootstrapServers already defaults to a local address and
        // nothing here connects, so no configuration is needed to exercise the registration.
        _ = services.AddKafkaTransport(TransportName, _ => { });

        await AssertDecodesAsync(services, "Kafka");
    }

    /// <summary>
    /// Resolves the receiver a consumer of this transport would resolve, and asserts the registration
    /// wrapped it in the decoding decorator.
    /// </summary>
    /// <remarks>
    /// The failure message names the consequence rather than the mechanism, because the mechanism is
    /// exactly what must be free to change without invalidating this arm.
    /// </remarks>
    private static async Task AssertDecodesAsync(IServiceCollection services, string transportName)
    {
        // await using: the decorator is IAsyncDisposable, and the container refuses to dispose such a
        // singleton synchronously. A sync using here fails AFTER a successful resolve, which reads as a
        // wiring failure and is not one.
        await using var provider = services.BuildServiceProvider();

        var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(TransportName);

        _ = receiver.ShouldBeOfType<CloudEventDecodingTransportReceiver>(
            $"{transportName}'s registration claims inbound CloudEvents support. The receiver a consumer "
            + "resolves came back undecorated, so every CloudEvent arriving on this transport would pass "
            + "through as an ordinary message — silently, and indistinguishably from one that never "
            + "carried CloudEvents markers.");
    }
}
