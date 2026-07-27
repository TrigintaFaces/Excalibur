// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Real-infrastructure conformance tests for the IBM MQ transport (bead <c>s8j0qx</c>) against a live
/// <c>icr.io/ibm-messaging/mq</c> queue manager. Exercises the <strong>real</strong> committed
/// <c>IbmMqTransportSender</c> (put under <c>MQPMO_SYNCPOINT</c> + commit) and
/// <c>IbmMqTransportReceiver</c> (the SA-ruled unit-of-work-per-message: a dedicated queue-manager
/// connection + <c>MQGMO_SYNCPOINT</c> get per in-flight message), asserting the emitted
/// put → get → acknowledge(commit) round-trip over the real IBM MQ .NET client.
/// </summary>
/// <remarks>
/// <para>
/// IBM MQ is point-to-point with a durable queue, so a message put before the receiver connects is retained
/// and delivered — no subscribe-before-send warm-up is required (unlike MQTT). Acknowledge commits the
/// syncpoint (removing the message); a reject backs it out for redelivery.
/// </para>
/// <para>
/// The queue manager runs in an <see cref="IbmMqContainerFixture"/> class fixture (its startup far exceeds
/// the base class's 30-second per-test budget) and soft-skips when Docker is unavailable, matching the other
/// transport conformance suites; the lock is authored non-skipped and runs authoritatively on the CI shard.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Transport")]
[Trait("Transport", "IbmMq")]
public sealed class IbmMqTransportConformanceTests
    : TransportConformanceTestBase<IbmMqChannelSender, IbmMqChannelReceiver>, IClassFixture<IbmMqContainerFixture>
{
    private const string TransportName = "conformance";

    private readonly IbmMqContainerFixture _fixture;

    private ServiceProvider? _provider;

    public IbmMqTransportConformanceTests(IbmMqContainerFixture fixture) => _fixture = fixture;

    private ServiceProvider Provider()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException("IBM MQ container is not available.");
        }

        return _provider ??= new ServiceCollection()
            .AddLogging()
            .AddIbmMqTransport(TransportName, mq =>
            {
                mq.QueueManager = IbmMqContainerFixture.QueueManagerName;
                mq.Host = _fixture.Host;
                mq.Port = _fixture.Port;
                mq.Channel = IbmMqContainerFixture.Channel;
                mq.QueueName = IbmMqContainerFixture.QueueName;
                mq.UserId = IbmMqContainerFixture.AppUser;
                mq.Password = IbmMqContainerFixture.AppPassword;
            })
            .BuildServiceProvider();
    }

    protected override Task<IbmMqChannelSender> CreateSenderAsync()
    {
        var sender = Provider().GetRequiredKeyedService<ITransportSender>(TransportName);
        return Task.FromResult(new IbmMqChannelSender(sender));
    }

    protected override Task<IbmMqChannelReceiver> CreateReceiverAsync()
    {
        var receiver = Provider().GetRequiredKeyedService<ITransportReceiver>(TransportName);
        return Task.FromResult(new IbmMqChannelReceiver(receiver));
    }

    protected override Task<IDeadLetterQueueManager?> CreateDlqManagerAsync() =>
        Task.FromResult<IDeadLetterQueueManager?>(null); // DLQ is a decorator concern, not this W2 primitive.

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
/// <see cref="IChannelSender"/> adapter over the real committed IBM MQ <see cref="ITransportSender"/>:
/// serializes the test message to a JSON body and puts it (under syncpoint + commit) as a
/// <see cref="TransportMessage"/>.
/// </summary>
public sealed class IbmMqChannelSender(ITransportSender sender) : IChannelSender
{
    public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var result = await sender
            .SendAsync(new TransportMessage { Body = body }, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"IBM MQ put failed: {result.Error?.Message}");
        }
    }
}

/// <summary>
/// <see cref="IChannelReceiver"/> adapter over the real committed IBM MQ <see cref="ITransportReceiver"/>:
/// gets one message under its own syncpoint, deserializes the JSON body, and acknowledges it (commits the
/// syncpoint), exercising the unit-of-work-per-message get + commit path against the real queue manager.
/// </summary>
public sealed class IbmMqChannelReceiver(ITransportReceiver receiver) : IChannelReceiver
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
}

/// <summary>
/// Starts a single <c>icr.io/ibm-messaging/mq</c> developer queue manager shared by the IBM MQ conformance
/// class (queue manager <c>QM1</c>, channel <c>DEV.APP.SVRCONN</c>, queue <c>DEV.QUEUE.1</c>). Sets
/// <see cref="Available"/> to false when Docker is unavailable so the suite soft-skips consistently with the
/// other transport conformance suites.
/// </summary>
public sealed class IbmMqContainerFixture : IAsyncLifetime
{
    internal const string QueueManagerName = "QM1";
    internal const string Channel = "DEV.APP.SVRCONN";
    internal const string QueueName = "DEV.QUEUE.1";
    internal const string AppUser = "app";
    internal const string AppPassword = "passw0rd";

    private IContainer? _container;

    /// <summary>The queue-manager host once the container is running.</summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>The mapped listener port once the container is running.</summary>
    public int Port { get; private set; }

    /// <summary>Whether the IBM MQ container started successfully.</summary>
    public bool Available { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder()
                .WithImage("icr.io/ibm-messaging/mq:latest")
                .WithPortBinding(1414, assignRandomHostPort: true)
                .WithEnvironment("LICENSE", "accept")
                .WithEnvironment("MQ_QMGR_NAME", QueueManagerName)
                .WithEnvironment("MQ_APP_PASSWORD", AppPassword)
                // The developer image logs this line once the queue manager + dev objects are ready.
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilMessageIsLogged("Started queue manager"))
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync().ConfigureAwait(false);
            Host = _container.Hostname;
            Port = _container.GetMappedPublicPort(1414);
            Available = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.WriteLine($"IBM MQ container unavailable: {ex.Message}");
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
