// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Real-infrastructure conformance tests for the MQTT transport (bead <c>opmjmz</c>) against a live
/// <c>eclipse-mosquitto</c> broker. Exercises the <strong>real</strong> committed
/// <c>MqttTransportSender</c>/<c>MqttTransportReceiver</c> — resolved through the real
/// <c>AddMqttTransport</c> DI registration over the real MQTTnet client — and asserts the emitted
/// publish → subscribe → acknowledge round-trip at QoS 1.
/// </summary>
/// <remarks>
/// <para>
/// MQTT is publish/subscribe with a clean session by default, so the subscription MUST exist before the
/// first publish or the message is dropped by the broker. <see cref="CreateReceiverAsync"/> therefore warms
/// the subscription (a bounded best-effort receive) before any test sends — the MQTT analogue of Pulsar's
/// resolve-time consumer creation.
/// </para>
/// <para>
/// The broker runs in a <see cref="MosquittoContainerFixture"/> class fixture and soft-skips when Docker is
/// unavailable, matching the other transport conformance suites (Kafka/RabbitMQ/Pulsar); the lock is
/// authored non-skipped and runs authoritatively on the CI integration shard.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Transport")]
[Trait("Transport", "Mqtt")]
public sealed class MqttTransportConformanceTests
    : TransportConformanceTestBase<MqttChannelSender, MqttChannelReceiver>, IClassFixture<MosquittoContainerFixture>
{
    private const string TransportName = "conformance";

    private readonly MosquittoContainerFixture _fixture;
    private readonly string _topic = $"excalibur/conformance/{Guid.NewGuid():N}";

    private ServiceProvider? _provider;

    public MqttTransportConformanceTests(MosquittoContainerFixture fixture) => _fixture = fixture;

    private ServiceProvider Provider()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException("Mosquitto container is not available.");
        }

        return _provider ??= new ServiceCollection()
            .AddLogging()
            .AddMqttTransport(TransportName, mqtt =>
            {
                mqtt.Host = _fixture.Host;
                mqtt.Port = _fixture.Port;
                mqtt.ClientId = $"conformance-{Guid.NewGuid():N}";
                mqtt.Topic = _topic;
                mqtt.QualityOfService = MqttQualityOfService.AtLeastOnce;

                // The Mosquitto container listens in the clear, so the secure-by-default posture is
                // opted out here deliberately.
                mqtt.RequireTls = false;
            })
            .BuildServiceProvider();
    }

    protected override Task<MqttChannelSender> CreateSenderAsync()
    {
        var sender = Provider().GetRequiredKeyedService<ITransportSender>(TransportName);
        return Task.FromResult(new MqttChannelSender(sender));
    }

    protected override async Task<MqttChannelReceiver> CreateReceiverAsync()
    {
        var receiver = Provider().GetRequiredKeyedService<ITransportReceiver>(TransportName);
        var adapter = new MqttChannelReceiver(receiver);

        // Warm the subscription BEFORE any test publishes (clean-session MQTT drops messages published to a
        // topic with no live subscriber). A short bounded receive triggers the lazy connect + subscribe.
        await adapter.WarmSubscriptionAsync().ConfigureAwait(false);
        return adapter;
    }

    protected override Task<IDeadLetterQueueManager?> CreateDlqManagerAsync() =>
        Task.FromResult<IDeadLetterQueueManager?>(null); // DLQ is a decorator concern, not this W1 primitive.

    /// <summary>
    /// SAFETY, over a live broker. Two QoS-1 deliveries that share a correlation id are settled
    /// independently: acknowledging each in turn succeeds, because each holds its own handle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delivery handle used to be keyed by the correlation data. A correlation id is <b>shared by
    /// related messages by design</b>, so two ordinary related publishes collided: the second replaced the
    /// first, acknowledging the first acknowledged the <b>second</b>, and the first was left with no handle
    /// at all. The unit arms in <c>MqttTransportReceiverShould</c> bind that at the receive path with a
    /// stubbed acknowledge callback; this one puts the same property through the <b>real</b> MQTTnet client,
    /// the real <c>AddMqttTransport</c> registration and a real <c>eclipse-mosquitto</c> broker, because the
    /// defect was in how a real PUBACK was routed and a stub cannot fault that.
    /// </para>
    /// <para>
    /// <b>What this arm does NOT cover, stated so nobody reads it as more than it is.</b> It does not prove
    /// redelivery-on-session-resume for the unacknowledged sibling: that needs a persistent session plus a
    /// reconnect. The property bound here is <i>each delivery carries its own settlement handle end to end</i>,
    /// not <i>a withheld acknowledgement is later redelivered</i>.
    /// </para>
    /// <para>
    /// That second property is <b>no longer uncovered</b>, and this note used to say it was. The registration
    /// now connects with a persistent session, and <see cref="MqttSessionRedeliveryTests"/> binds the
    /// round trip over the same real broker: reject, disconnect, resume on the same client id, receive it
    /// exactly once — with an acknowledged-message control so "redeliver everything" cannot pass. This
    /// paragraph is kept rather than deleted because the gap it described was real, and a coverage note that
    /// silently disappears leaves the next reader unable to tell whether it was closed or forgotten.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Settle_two_shared_correlation_deliveries_independently_over_a_real_broker()
    {
        Assert.SkipUnless(_fixture.Available, "Mosquitto container unavailable.");

        const string SharedCorrelation = "conformance-correlation";
        var sender = Provider().GetRequiredKeyedService<ITransportSender>(TransportName);
        var receiver = Provider().GetRequiredKeyedService<ITransportReceiver>(TransportName);

        // Subscribe before publishing — clean-session MQTT drops anything published to a topic with no live
        // subscriber, which would empty this arm rather than fail it. ReceiveAsync blocks until a message
        // arrives OR the token cancels, and nothing has been published yet, so this priming call is given a
        // short token and is EXPECTED to cancel: what it is here for is the subscribe it performs on the way
        // in, not a message. Passing CancellationToken.None here hangs the run forever instead of failing it.
        using (var prime = new CancellationTokenSource(TimeSpan.FromSeconds(5))) // deadline-ok: this priming receive is EXPECTED to cancel -- the subscribe is the point, and an unbounded token hangs the run
        {
            try
            {
                _ = await receiver.ReceiveAsync(maxMessages: 1, prime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: the subscribe happened, no message could have arrived yet.
            }
        }

        foreach (var tag in new[] { "first", "second" })
        {
            var result = await sender.SendAsync(
                new TransportMessage
                {
                    Body = Encoding.UTF8.GetBytes(tag),
                    CorrelationId = SharedCorrelation,
                },
                CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.IsSuccess, $"publishing '{tag}' failed: {result.Error?.Message}");
        }

        var received = await ReceiveBothAsync(receiver).ConfigureAwait(false);

        Assert.Equal(2, received.Count);

        // The condition under test: both really do carry the same correlation, and yet their delivery
        // identities differ. Equal ids here would mean the collision is back.
        Assert.All(received, m => Assert.Equal(SharedCorrelation, m.CorrelationId));
        Assert.NotEqual(received[0].Id, received[1].Id);

        // LIVENESS as well as safety: BOTH settle. With the defect the second acknowledgement had no handle
        // left to find, so an arm that acknowledged only one would pass against it.
        await receiver.AcknowledgeAsync(received[0], CancellationToken.None).ConfigureAwait(false);
        await receiver.AcknowledgeAsync(received[1], CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// SAFETY, over a live broker at QoS 2. The same independence holds under the exactly-once delivery
    /// flow, whose settle is a different protocol exchange from QoS 1's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The arm above runs at QoS 1, where settling is a single PUBACK. QoS 2 settles with a four-packet
    /// handshake (PUBLISH / PUBREC / PUBREL / PUBCOMP) that the client tracks per packet, so this is the
    /// tier where routing the wrong handle does the most damage: the acknowledgement is not just applied to
    /// the wrong message, it advances the wrong packet's state machine. QoS 2 is also the only level at
    /// which this transport offers exactly-once end to end, which is precisely when a consumer is relying
    /// on a settle meaning what it says.
    /// </para>
    /// <para>
    /// It builds its own registration rather than reusing <see cref="Provider"/>, because the quality of
    /// service is fixed at registration and the shared provider is memoized at QoS 1 for the base
    /// conformance suite. Its own topic keeps the two from reading each other's publishes.
    /// </para>
    /// <para>
    /// <b>Scope, stated so this is not read as more than it is.</b> Like the QoS-1 arm it connects with a
    /// clean session, so it proves <i>each delivery carries its own settlement handle end to end</i>, not
    /// <i>a withheld acknowledgement is redelivered on session resume</i>. That second property needs
    /// <c>CleanSession=false</c> plus a reconnect and remains uncovered at this tier.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Settle_two_shared_correlation_deliveries_independently_over_a_real_broker_at_qos2()
    {
        Assert.SkipUnless(_fixture.Available, "Mosquitto container unavailable.");

        const string SharedCorrelation = "conformance-correlation-qos2";
        const string Qos2TransportName = "conformance-qos2";

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddMqttTransport(Qos2TransportName, mqtt =>
            {
                mqtt.Host = _fixture.Host;
                mqtt.Port = _fixture.Port;
                mqtt.ClientId = $"conformance-qos2-{Guid.NewGuid():N}";
                mqtt.Topic = $"excalibur/conformance/qos2/{Guid.NewGuid():N}";
                mqtt.QualityOfService = MqttQualityOfService.ExactlyOnce;

                // The Mosquitto container listens in the clear, so the secure-by-default posture is
                // opted out here deliberately.
                mqtt.RequireTls = false;
            })
            .BuildServiceProvider();

        var sender = provider.GetRequiredKeyedService<ITransportSender>(Qos2TransportName);
        var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(Qos2TransportName);

        // Subscribe before publishing — clean-session MQTT drops anything published to a topic with no live
        // subscriber, which would empty this arm rather than fail it. ReceiveAsync blocks until a message
        // arrives OR the token cancels, and nothing has been published yet, so this priming call is given a
        // short token and is EXPECTED to cancel: what it is here for is the subscribe it performs on the way
        // in, not a message. Passing CancellationToken.None here hangs the run forever instead of failing it.
        using (var prime = new CancellationTokenSource(TimeSpan.FromSeconds(5))) // deadline-ok: this priming receive is EXPECTED to cancel -- the subscribe is the point, and an unbounded token hangs the run
        {
            try
            {
                _ = await receiver.ReceiveAsync(maxMessages: 1, prime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: the subscribe happened, no message could have arrived yet.
            }
        }

        foreach (var tag in new[] { "first", "second" })
        {
            var result = await sender.SendAsync(
                new TransportMessage
                {
                    Body = Encoding.UTF8.GetBytes(tag),
                    CorrelationId = SharedCorrelation,
                },
                CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.IsSuccess, $"publishing '{tag}' at QoS 2 failed: {result.Error?.Message}");
        }

        var received = await ReceiveBothAsync(receiver).ConfigureAwait(false);

        Assert.Equal(2, received.Count);

        // The condition under test: both really do carry the same correlation, and yet their delivery
        // identities differ. Equal ids here would mean the collision is back.
        Assert.All(received, m => Assert.Equal(SharedCorrelation, m.CorrelationId));
        Assert.NotEqual(received[0].Id, received[1].Id);

        // LIVENESS as well as safety: BOTH settle, each completing its own PUBREL/PUBCOMP exchange. With the
        // defect the second acknowledgement had no handle left to find, so an arm that settled only one
        // would pass against it.
        await receiver.AcknowledgeAsync(received[0], CancellationToken.None).ConfigureAwait(false);
        await receiver.AcknowledgeAsync(received[1], CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Drains until both publishes have arrived, bounded — a broker may deliver them across separate reads.
    /// </summary>
    private static async Task<List<TransportReceivedMessage>> ReceiveBothAsync(ITransportReceiver receiver)
    {
        var received = new List<TransportReceivedMessage>();

        // EVERY receive is bounded. ReceiveAsync blocks on an empty buffer until its token cancels, so an
        // unbounded token here does not make the arm slow, it makes it hang: the run never reports at all,
        // which is strictly worse than a failure because nothing is left to read.
        for (var attempt = 0; attempt < 20 && received.Count < 2; attempt++)
        {
            using var attemptTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // deadline-ok: per-attempt bound inside a 20-attempt loop; ReceiveAsync blocks on an empty buffer until its token cancels

            try
            {
                var batch = await receiver.ReceiveAsync(maxMessages: 2, attemptTimeout.Token).ConfigureAwait(false);
                received.AddRange(batch);
            }
            catch (OperationCanceledException)
            {
                // No delivery within this attempt's window; fall through and try again.
            }
        }

        return received;
    }

    protected override async Task DisposeTransportAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync().ConfigureAwait(false);
            _provider = null;
        }
    }
}

/// <summary>
/// <see cref="IChannelSender"/> adapter over the real committed MQTT <see cref="ITransportSender"/>: serializes
/// the test message to a JSON body and publishes it as a <see cref="TransportMessage"/>.
/// </summary>
public sealed class MqttChannelSender(ITransportSender sender) : IChannelSender
{
    public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var result = await sender
            .SendAsync(new TransportMessage { Body = body }, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"MQTT publish failed: {result.Error?.Message}");
        }
    }
}

/// <summary>
/// <see cref="IChannelReceiver"/> adapter over the real committed MQTT <see cref="ITransportReceiver"/>:
/// receives one message, deserializes the JSON body, and acknowledges it (PUBACK), exercising both the
/// round-trip and the acknowledge path against the real broker.
/// </summary>
public sealed class MqttChannelReceiver(ITransportReceiver receiver) : IChannelReceiver
{
    public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
    {
        var messages = await receiver.ReceiveAsync(maxMessages: 1, cancellationToken).ConfigureAwait(false);
        if (messages.Count == 0)
        {
            return default;
        }

        var message = messages[0];
        var result = JsonSerializer.Deserialize<T>(message.Body.Span);
        await receiver.AcknowledgeAsync(message, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>Forces the lazy connect + subscribe so the subscription exists before the first publish.</summary>
    internal async Task WarmSubscriptionAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            _ = await receiver.ReceiveAsync(maxMessages: 1, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected: no message yet — the subscribe side-effect is what we needed.
        }
    }
}

/// <summary>
/// Starts a single <c>eclipse-mosquitto</c> broker shared by the MQTT conformance class, configured to accept
/// anonymous connections on 1883. Sets <see cref="Available"/> to false when Docker is unavailable so the
/// suite soft-skips consistently with the other transport conformance suites.
/// </summary>
public sealed class MosquittoContainerFixture : IAsyncLifetime
{
    private const string MosquittoConf = "listener 1883\nallow_anonymous true\n";

    private IContainer? _container;

    /// <summary>The broker host once the container is running.</summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>The mapped broker port once the container is running.</summary>
    public int Port { get; private set; }

    /// <summary>Whether the mosquitto container started successfully.</summary>
    public bool Available { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder()
                .WithImage("eclipse-mosquitto:2")
                .WithPortBinding(1883, assignRandomHostPort: true)
                // Mosquitto 2.x is local-only + no-anonymous by default; supply a minimal listener config.
                .WithResourceMapping(
                    Encoding.UTF8.GetBytes(MosquittoConf), "/mosquitto/config/mosquitto.conf")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mosquitto version"))
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync().ConfigureAwait(false);
            Host = _container.Hostname;
            Port = _container.GetMappedPublicPort(1883);
            Available = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.WriteLine($"Mosquitto container unavailable: {ex.Message}");
            Available = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
