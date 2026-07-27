// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Real-infrastructure conformance tests for the Apache Pulsar transport (beads `op3nwj`/`xkxuds`) against a
/// live <c>apachepulsar/pulsar</c> standalone container. Closes the W1 Pulsar coverage gap: the shipped
/// transport had ZERO tests, so this exercises the <strong>real</strong> committed
/// <c>PulsarTransportSender</c>/<c>PulsarTransportReceiver</c> — resolved through the real
/// <c>AddPulsarTransport</c> DI registration (keyed <see cref="ITransportSender"/>/<see cref="ITransportReceiver"/>)
/// over the real DotPulsar client — and asserts the emitted send → receive → acknowledge round-trip.
/// </summary>
/// <remarks>
/// <para>
/// The Pulsar container is started by the <see cref="PulsarContainerFixture"/> (an <see cref="IClassFixture{T}"/>)
/// rather than inside the harness's <c>CreateSenderAsync</c>: Pulsar standalone startup routinely exceeds the
/// base class's 30-second per-test initialization budget, so starting it in the class fixture (which has no
/// such cap) keeps the round-trip deterministic instead of intermittently soft-skipping. Matches the
/// Docker-unavailable soft-skip convention of the other transport conformance suites (Kafka/RabbitMQ).
/// </para>
/// <para>
/// Coverage inherited from <see cref="TransportConformanceTestBase{TSender, TReceiver}"/>: basic send/receive
/// round-trip (R2.1), body-level metadata preservation (R2.2), 100-message concurrency with no loss (R2.3),
/// and graceful shutdown + restart (R15.2) — all against real Pulsar.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Transport")]
[Trait("Transport", "Pulsar")]
public sealed class PulsarTransportConformanceTests
	: TransportConformanceTestBase<PulsarChannelSender, PulsarChannelReceiver>, IClassFixture<PulsarContainerFixture>
{
	private const string TransportName = "conformance";

	private readonly PulsarContainerFixture _fixture;
	private readonly string _topic = $"persistent://public/default/conformance-{Guid.NewGuid():N}";
	private readonly string _subscription = $"conformance-sub-{Guid.NewGuid():N}";

	private ServiceProvider? _provider;

	public PulsarTransportConformanceTests(PulsarContainerFixture fixture) => _fixture = fixture;

	private ServiceProvider Provider()
	{
		if (!_fixture.Available)
		{
			throw new InvalidOperationException("Pulsar container is not available.");
		}

		return _provider ??= new ServiceCollection()
			.AddLogging()
			.AddPulsarTransport(TransportName, pulsar => pulsar
				.ServiceUrl(_fixture.ServiceUrl)
				.Topic(_topic)
				.SubscriptionName(_subscription)
				.SubscriptionType(global::Excalibur.Dispatch.Transport.Pulsar.PulsarSubscriptionType.Shared))
			.BuildServiceProvider();
	}

	protected override Task<PulsarChannelSender> CreateSenderAsync()
	{
		var sender = Provider().GetRequiredKeyedService<ITransportSender>(TransportName);
		return Task.FromResult(new PulsarChannelSender(sender));
	}

	protected override Task<PulsarChannelReceiver> CreateReceiverAsync()
	{
		// Resolving the receiver creates + subscribes the DotPulsar consumer now, BEFORE any test sends,
		// so the subscription exists when messages are published (round-trip is deterministic).
		var receiver = Provider().GetRequiredKeyedService<ITransportReceiver>(TransportName);
		return Task.FromResult(new PulsarChannelReceiver(receiver));
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
/// <see cref="IChannelSender"/> adapter over the real committed <see cref="ITransportSender"/> Pulsar
/// primitive: serializes the test message to a JSON body and sends it as a <see cref="TransportMessage"/>.
/// </summary>
public sealed class PulsarChannelSender(ITransportSender sender) : IChannelSender
{
	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		var body = JsonSerializer.SerializeToUtf8Bytes(message);
		var result = await sender
			.SendAsync(new TransportMessage { Body = body }, cancellationToken).ConfigureAwait(false);
		if (!result.IsSuccess)
		{
			throw new InvalidOperationException($"Pulsar send failed: {result.Error?.Message}");
		}
	}
}

/// <summary>
/// <see cref="IChannelReceiver"/> adapter over the real committed <see cref="ITransportReceiver"/> Pulsar
/// primitive: receives one message, deserializes the JSON body, and acknowledges it (so the round-trip and
/// the acknowledge path are both exercised against real Pulsar).
/// </summary>
public sealed class PulsarChannelReceiver(ITransportReceiver receiver) : IChannelReceiver
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
/// Starts a single <c>apachepulsar/pulsar</c> standalone container shared by the Pulsar conformance class.
/// Runs in the class-fixture lifecycle (no per-test init cap) because Pulsar standalone startup is slow.
/// Sets <see cref="Available"/> to false when Docker is unavailable so the suite soft-skips consistently
/// with the other transport conformance suites.
/// </summary>
public sealed class PulsarContainerFixture : IAsyncLifetime
{
	private IContainer? _container;

	/// <summary>The <c>pulsar://host:port</c> broker URL once the container is running.</summary>
	public string ServiceUrl { get; private set; } = string.Empty;

	/// <summary>Whether the Pulsar container started successfully.</summary>
	public bool Available { get; private set; }

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new ContainerBuilder()
				.WithImage("apachepulsar/pulsar:3.3.1")
				.WithPortBinding(6650, assignRandomHostPort: true)
				.WithPortBinding(8080, assignRandomHostPort: true)
				// -nfw: no functions worker, -nss: no stream storage → materially faster standalone startup.
				.WithCommand("bin/pulsar", "standalone", "-nfw", "-nss")
				// a4xt9d: wait on the BROKER HEALTH endpoint, not /admin/v2/clusters. /clusters answers 200 as
				// soon as the metadata store is up — before the broker can actually serve producers/consumers on
				// 6650, so send/receive tests raced container readiness (~1-4 fails at ~8.7s under shard load).
				// /admin/v2/brokers/health returns 200 only after the broker round-trips a message through its own
				// produce+consume path (Apache Pulsar's documented "wait for standalone to finish starting" probe),
				// so a 200 guarantees the send/receive path these tests exercise is actually ready.
				.WithWaitStrategy(Wait.ForUnixContainer()
					.UntilHttpRequestIsSucceeded(request => request.ForPath("/admin/v2/brokers/health").ForPort(8080)))
				.WithCleanUp(true)
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			ServiceUrl = $"pulsar://{_container.Hostname}:{_container.GetMappedPublicPort(6650)}";
			Available = true;
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			Console.WriteLine($"Pulsar container unavailable: {ex.Message}");
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
