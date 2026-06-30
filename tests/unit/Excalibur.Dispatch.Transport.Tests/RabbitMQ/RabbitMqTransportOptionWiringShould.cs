// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Categories;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Regression tests for the RabbitMQ transport wired-path option gaps (o0wv4k):
/// the configured QoS prefetch default and the connection-recovery options must be
/// honored instead of being hardcoded / disabled.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqTransportOptionWiringShould : UnitTestBase
{
	[Fact]
	public async Task ApplyDefaultPrefetch_WhenNoQueuePrefetchIsConfigured()
	{
		// Arrange -- no ConfigureQueue, so the subscriber falls back to the documented default (100).
		var channel = A.Fake<IChannel>();
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddRabbitMQTransport("test", rmq =>
		{
			_ = rmq.ConnectionString("amqp://guest:guest@localhost:5672/");
		});

		// Override the transport's real IChannel with a fake (registered last so it wins resolution),
		// so the subscriber applies QoS against the fake instead of opening a live broker connection.
		services.AddSingleton(channel);

		await using var provider = services.BuildServiceProvider();
		var subscriber = provider.GetRequiredKeyedService<ITransportSubscriber>("test");

		// Act -- a pre-cancelled token lets SubscribeAsync apply QoS then unwind immediately.
		// The unwind surfaces as OperationCanceledException, which is expected and swallowed; the
		// behavior under test is that BasicQosAsync was invoked with the resolved default prefetch.
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		try
		{
			await subscriber.SubscribeAsync(
				(_, _) => Task.FromResult(MessageAction.Acknowledge),
				cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected: the pre-cancelled token unwinds the subscribe loop after QoS is applied.
		}

		// Assert -- BasicQosAsync was invoked with the default prefetch of 100, not 0 (disabled).
		A.CallTo(() => channel.BasicQosAsync(
			0u, (ushort)100, false, A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public void HonorConnectionRecoveryOptions_OnTheConnectionFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddRabbitMQTransport("test", rmq =>
		{
			_ = rmq.ConnectionString("amqp://guest:guest@localhost:5672/")
				.AutomaticRecovery(enabled: false, networkRecoveryInterval: TimeSpan.FromSeconds(42));
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IConnectionFactory>();

		// Assert -- mapped from options, not the hardcoded true / 10s.
		var rabbitFactory = factory.ShouldBeOfType<ConnectionFactory>();
		rabbitFactory.AutomaticRecoveryEnabled.ShouldBeFalse();
		rabbitFactory.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	public void SetRecoveryOptions_WhenAutomaticRecoveryConfiguredOnBuilder()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.AutomaticRecovery(enabled: false, networkRecoveryInterval: TimeSpan.FromSeconds(42));

		// Assert
		options.Connection.AutomaticRecoveryEnabled.ShouldBeFalse();
		options.Connection.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	public void RetainRecoveryInterval_WhenAutomaticRecoveryIntervalIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act -- null interval retains the existing configured value (default 10s).
		_ = builder.AutomaticRecovery(enabled: true);

		// Assert
		options.Connection.AutomaticRecoveryEnabled.ShouldBeTrue();
		options.Connection.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void ThrowWhenAutomaticRecoveryIntervalIsZero()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(
			() => builder.AutomaticRecovery(enabled: true, networkRecoveryInterval: TimeSpan.Zero));
	}

	[Fact]
	public void ThrowWhenAutomaticRecoveryIntervalIsNegative()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(
			() => builder.AutomaticRecovery(enabled: true, networkRecoveryInterval: TimeSpan.FromSeconds(-1)));
	}
}
