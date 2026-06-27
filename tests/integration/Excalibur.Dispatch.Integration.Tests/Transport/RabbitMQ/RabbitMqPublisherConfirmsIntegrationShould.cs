// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.RabbitMQ;

/// <summary>
/// NON-SKIPPED real-RabbitMQ regression lock for bead <c>fjtok4</c> (sprint 855, SA-ruling-A): the unified
/// keyed RabbitMQ <see cref="ITransportSender"/> MUST provide <b>publisher confirms</b> (the publish
/// awaits the broker ack — at-least-once) and surface an <b>unroutable</b> mandatory publish as
/// <c>SendResult.Failure(Code="Unroutable")</c> — eliminating the advertised-but-inert publisher-confirms
/// defect (the off-by-default shared channel was at-most-once and returned false success for unroutable).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the impl (<c>issue-remediation-protocol</c>). Platform's fjtok4 impl gives the
/// sender a dedicated confirms+tracking channel (<c>CreateChannelOptions(true, true)</c>) so
/// <c>BasicPublishAsync</c> awaits the ack, and publishes <c>mandatory: true</c> with a
/// <c>BasicReturnAsync</c> correlation (AMQP delivers <c>basic.return</c> BEFORE the confirm) so an
/// unroutable message becomes a non-retryable <c>Failure</c> instead of a false <c>Success</c>.
/// </para>
/// <para>
/// <b>Real-infra, NON-SKIPPED</b> (NFR-1): runs against real RabbitMQ via <see cref="RabbitMqContainerFixture"/>.
/// <c>DockerAvailable</c> is asserted (hard requirement) — no silent skip. Asserts the <i>observed</i> broker
/// behavior (ack-awaited + persisted on a bound queue; broker-returned on an unbound exchange), not that an
/// option was set. The internal <c>RabbitMqTransportSender</c> is constructed via reflection (mirrors
/// <c>KafkaTransportSenderIntegrationShould</c>) on a confirms-enabled channel.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix sender used the off-by-default shared
/// channel (no confirm await) and <c>mandatory: false</c>, so an unroutable publish returned
/// <c>Success</c> — the unbound-exchange fact below would fail (got Success, expected Unroutable). GREEN
/// once the dedicated confirms channel + mandatory/return correlation are wired.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "RabbitMQ")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMqPublisherConfirmsIntegrationShould : IClassFixture<RabbitMqContainerFixture>
{
	private readonly RabbitMqContainerFixture _fixture;

	public RabbitMqPublisherConfirmsIntegrationShould(RabbitMqContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ReturnSuccessAndPersist_WhenPublishedToABoundQueue_ConfirmAwaited()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"RabbitMQ must be available — real-broker publisher-confirms proof (NFR-1)");

		await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
		await using var channel = await CreateConfirmsChannelAsync(connection).ConfigureAwait(false);

		var exchange = $"ex-bound-{Guid.NewGuid():N}";
		var queue = $"q-bound-{Guid.NewGuid():N}";
		const string routingKey = "rk-bound";
		await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: false, autoDelete: true).ConfigureAwait(false);
		_ = await channel.QueueDeclareAsync(queue, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
		await channel.QueueBindAsync(queue, exchange, routingKey).ConfigureAwait(false);

		await using var sender = CreateSender(channel, destination: queue, exchange: exchange, defaultRoutingKey: routingKey);
		var body = Encoding.UTF8.GetBytes("""{"order":"fjtok4"}""");
		var message = new TransportMessage
		{
			Id = Guid.NewGuid().ToString(),
			Body = body,
			ContentType = "application/json",
			MessageType = "OrderPlaced",
		};

		// Act
		var result = await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert — the confirm was awaited (at-least-once), and the broker actually routed + stored it.
		result.IsSuccess.ShouldBeTrue();
		result.MessageId.ShouldBe(message.Id);

		var delivered = await channel.BasicGetAsync(queue, autoAck: true).ConfigureAwait(false);
		delivered.ShouldNotBeNull();
		delivered.Body.ToArray().ShouldBe(body);
	}

	[Fact]
	public async Task ReturnUnroutableFailure_WhenNoQueueBound_MandatoryReturnCorrelated()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"RabbitMQ must be available — real-broker publisher-confirms proof (NFR-1)");

		await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
		await using var channel = await CreateConfirmsChannelAsync(connection).ConfigureAwait(false);

		// A direct exchange with NO queue bound — a mandatory publish is returned by the broker.
		var exchange = $"ex-unbound-{Guid.NewGuid():N}";
		await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: false, autoDelete: true).ConfigureAwait(false);

		await using var sender = CreateSender(channel, destination: exchange, exchange: exchange, defaultRoutingKey: "no-binding");
		var message = new TransportMessage
		{
			Id = Guid.NewGuid().ToString(),
			Body = Encoding.UTF8.GetBytes("unroutable"),
			ContentType = "text/plain",
		};

		// Act
		var result = await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert — unroutable surfaces as a non-retryable failure (pre-fix returned a false Success).
		result.IsSuccess.ShouldBeFalse();
		result.Error.ShouldNotBeNull();
		result.Error.Code.ShouldBe("Unroutable");
		result.Error.IsRetryable.ShouldBeFalse();
	}

	private async Task<IConnection> CreateConnectionAsync()
	{
		var factory = new ConnectionFactory { Uri = new Uri(_fixture.ConnectionString) };
		return await factory.CreateConnectionAsync().ConfigureAwait(false);
	}

	private static async Task<IChannel> CreateConfirmsChannelAsync(IConnection connection) =>
		await connection.CreateChannelAsync(
			new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true))
			.ConfigureAwait(false);

	// The RabbitMqTransportSender is internal — construct it via reflection on a confirms-enabled channel
	// (mirrors KafkaTransportSenderIntegrationShould). ctor: (IChannel, destination, exchange, routingKey, ILogger<>).
	private static ITransportSender CreateSender(IChannel channel, string destination, string exchange, string defaultRoutingKey)
	{
		var senderType = typeof(RabbitMqConsumerOptions).Assembly
			.GetType("Excalibur.Dispatch.Transport.RabbitMQ.RabbitMqTransportSender")!;
		var loggerOfT = typeof(Logger<>).MakeGenericType(senderType);
		var logger = Activator.CreateInstance(loggerOfT, NullLoggerFactory.Instance)!;
		var ctor = senderType.GetConstructors()[0];
		var instance = ctor.Invoke([channel, destination, exchange, defaultRoutingKey, logger]);
		return (ITransportSender)instance;
	}
}
