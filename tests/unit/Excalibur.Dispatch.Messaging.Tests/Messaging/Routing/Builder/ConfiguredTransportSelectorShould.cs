// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// CA2012: FakeItEasy's .Returns() stores ValueTask internally - this is expected for test setup
#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="ConfiguredTransportSelector"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfiguredTransportSelectorShould
{
	#region Constructor tests

	[Fact]
	public void ThrowOnNullConfiguration()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ConfiguredTransportSelector(null!));
	}

	#endregion

	#region SelectTransportAsync tests

	[Fact]
	public async Task SelectDefaultTransportWhenNoRulesMatch()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport.Default("local");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var transport = await selector.SelectTransportAsync(message, context, CancellationToken.None);

		// Assert
		transport.ShouldBe("local");
	}

	[Fact]
	public async Task SelectTransportByMessageType()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Default("local");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var transport = await selector.SelectTransportAsync(message, context, CancellationToken.None);

		// Assert
		transport.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task SelectDifferentTransportsForDifferentMessageTypes()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Route<PaymentProcessedMessage>().To("kafka")
				.Default("local");
		});
		var context = CreateContext();

		// Act
		var orderTransport = await selector.SelectTransportAsync(
			new OrderCreatedMessage(), context, CancellationToken.None);
		var paymentTransport = await selector.SelectTransportAsync(
			new PaymentProcessedMessage(), context, CancellationToken.None);

		// Assert
		orderTransport.ShouldBe("rabbitmq");
		paymentTransport.ShouldBe("kafka");
	}

	[Fact]
	public async Task FallBackToDefaultForUnmatchedMessageType()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Default("local");
		});
		var message = new PaymentProcessedMessage();
		var context = CreateContext();

		// Act
		var transport = await selector.SelectTransportAsync(message, context, CancellationToken.None);

		// Assert
		transport.ShouldBe("local");
	}

	[Fact]
	public async Task EvaluateConditionalRule()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When(msg => msg.Amount > 1000).To("kafka")
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Default("local");
		});
		var context = CreateContext();

		// Act
		var highValueTransport = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None);
		var normalTransport = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);

		// Assert
		highValueTransport.ShouldBe("kafka");
		normalTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task EvaluateConditionalRuleWithContext()
	{
		// Arrange
		var premiumContext = A.Fake<IMessageContext>();
		A.CallTo(() => premiumContext.TenantId).Returns("premium");
		var standardContext = A.Fake<IMessageContext>();
		A.CallTo(() => standardContext.TenantId).Returns("standard");

		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When((msg, ctx) => ctx.TenantId == "premium").To("kafka")
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Default("local");
		});
		var message = new OrderCreatedMessage();

		// Act
		var premiumTransport = await selector.SelectTransportAsync(message, premiumContext, CancellationToken.None);
		var standardTransport = await selector.SelectTransportAsync(message, standardContext, CancellationToken.None);

		// Assert
		premiumTransport.ShouldBe("kafka");
		standardTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task CacheUnconditionalRuleResults()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Default("local");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act - call twice
		var first = await selector.SelectTransportAsync(message, context, CancellationToken.None);
		var second = await selector.SelectTransportAsync(message, context, CancellationToken.None);

		// Assert
		first.ShouldBe("rabbitmq");
		second.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task NotCacheConditionalRuleResults()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When(msg => msg.Amount > 1000).To("kafka")
				.Default("local");
		});
		var context = CreateContext();

		// Act - conditional rules should not be cached
		var highValue = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None);
		var lowValue = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);

		// Assert
		highValue.ShouldBe("kafka");
		lowValue.ShouldBe("local");
	}

	[Fact]
	public async Task UseLocalAsDefaultWhenNoDefaultConfigured()
	{
		// Arrange
		var selector = CreateSelector(_ => { }); // no configuration
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var transport = await selector.SelectTransportAsync(message, context, CancellationToken.None);

		// Assert
		transport.ShouldBe("local"); // RoutingConfiguration defaults to "local"
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		// Arrange
		var selector = CreateSelector(_ => { });
		var context = CreateContext();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await selector.SelectTransportAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		// Arrange
		var selector = CreateSelector(_ => { });
		var message = new OrderCreatedMessage();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await selector.SelectTransportAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task EvaluateRulesInRegistrationOrder()
	{
		// Arrange - first matching rule wins
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("first")
				.Route<OrderCreatedMessage>().To("second")
				.Default("local");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var transport = await selector.SelectTransportAsync(message, context, CancellationToken.None);

		// Assert
		transport.ShouldBe("first"); // first unconditional match wins
	}

	#endregion

	#region GetAvailableTransports tests

	[Fact]
	public void ReturnDefaultTransportWhenNoRulesConfigured()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport.Default("local");
		});

		// Act
		var transports = selector.GetAvailableTransports(typeof(OrderCreatedMessage));

		// Assert
		transports.ShouldContain("local");
	}

	[Fact]
	public void ReturnMatchingTransportsForMessageType()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Route<OrderCreatedMessage>().When(m => m.Amount > 1000).To("kafka")
				.Default("local");
		});

		// Act
		var transports = selector.GetAvailableTransports(typeof(OrderCreatedMessage)).ToList();

		// Assert
		transports.ShouldContain("local");
		transports.ShouldContain("rabbitmq");
		transports.ShouldContain("kafka");
	}

	[Fact]
	public void NotReturnTransportsForOtherMessageTypes()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Route<PaymentProcessedMessage>().To("kafka")
				.Default("local");
		});

		// Act
		var transports = selector.GetAvailableTransports(typeof(OrderCreatedMessage)).ToList();

		// Assert
		transports.ShouldContain("local");
		transports.ShouldContain("rabbitmq");
		transports.ShouldNotContain("kafka");
	}

	[Fact]
	public void ThrowOnNullMessageTypeForGetAvailableTransports()
	{
		// Arrange
		var selector = CreateSelector(_ => { });

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => selector.GetAvailableTransports(null!));
	}

	[Fact]
	public void DeduplicateTransports()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>().To("rabbitmq")
				.Route<OrderCreatedMessage>().When(m => m.Amount > 500).To("rabbitmq")
				.Default("local");
		});

		// Act
		var transports = selector.GetAvailableTransports(typeof(OrderCreatedMessage)).ToList();

		// Assert
		transports.Count(t => t == "rabbitmq").ShouldBe(1); // HashSet deduplicates
	}

	#endregion

	#region Helpers

	private static ConfiguredTransportSelector CreateSelector(Action<RoutingBuilder> configure)
	{
		var builder = new RoutingBuilder();
		configure(builder);
		var config = new RoutingConfiguration(builder);
		return new ConfiguredTransportSelector(config);
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	#endregion

	#region Test message types

	private sealed class OrderCreatedMessage : IIntegrationEvent
	{
		public decimal Amount { get; init; }
	}

	private sealed class PaymentProcessedMessage : IIntegrationEvent;

	#endregion
}
