// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
