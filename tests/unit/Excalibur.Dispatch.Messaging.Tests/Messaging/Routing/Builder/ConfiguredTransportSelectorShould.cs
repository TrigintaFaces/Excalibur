// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// CA2012: FakeItEasy's .Returns() stores ValueTask internally - this is expected for test setup
#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="ConfiguredTransportSelector"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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
		A.CallTo(() => premiumContext.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => premiumContext.Features).Returns(new Dictionary<Type, object>());
		premiumContext.GetOrCreateIdentityFeature().TenantId = "premium";

		var standardContext = A.Fake<IMessageContext>();
		A.CallTo(() => standardContext.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => standardContext.Features).Returns(new Dictionary<Type, object>());
		standardContext.GetOrCreateIdentityFeature().TenantId = "standard";

		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When((msg, ctx) => ctx.GetTenantId() == "premium").To("kafka")
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

	#region Conditional routing survives cache population

	// The cache is keyed by message TYPE, while a conditional rule is a predicate over the message
	// INSTANCE and the context. Populating the cache from an unconditional rule that was reached only
	// because an earlier conditional rule declined let the first message of a type decide the transport
	// for every later message of that type.

	[Fact]
	public async Task RouteAPremiumMessageToPremiumAfterAnOrdinaryMessageTookTheUnconditionalRule()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When(msg => msg.Amount > 1000).To("premium")
				.Route<OrderCreatedMessage>().To("ordinary")
				.Default("local");
		});
		var context = CreateContext();

		// Act -- ordinary first, which is what populates the cache.
		var ordinary = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);
		var premium = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None);

		// Assert
		ordinary.ShouldBe("ordinary");
		premium.ShouldBe("premium");
	}

	[Fact]
	public async Task HonourTheConditionalRuleAcrossRepeatedAlternation()
	{
		// Arrange
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When(msg => msg.Amount > 1000).To("premium")
				.Route<OrderCreatedMessage>().To("ordinary")
				.Default("local");
		});
		var context = CreateContext();
		var observed = new List<string>();

		// Act
		for (var round = 0; round < 4; round++)
		{
			observed.Add(await selector.SelectTransportAsync(
				new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None));
			observed.Add(await selector.SelectTransportAsync(
				new OrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None));
		}

		// Assert
		observed.ShouldBe(["ordinary", "premium", "ordinary", "premium", "ordinary", "premium", "ordinary", "premium"]);
	}

	[Fact]
	public async Task HonourAContextPredicateAfterAnEarlierMessageTookTheUnconditionalRule()
	{
		// Arrange
		var premiumContext = A.Fake<IMessageContext>();
		A.CallTo(() => premiumContext.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => premiumContext.Features).Returns(new Dictionary<Type, object>());
		premiumContext.GetOrCreateIdentityFeature().TenantId = "premium";

		var standardContext = A.Fake<IMessageContext>();
		A.CallTo(() => standardContext.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => standardContext.Features).Returns(new Dictionary<Type, object>());
		standardContext.GetOrCreateIdentityFeature().TenantId = "standard";

		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When((msg, ctx) => ctx.GetTenantId() == "premium").To("premium")
				.Route<OrderCreatedMessage>().To("ordinary")
				.Default("local");
		});

		// The same message instance, so only the context can distinguish the two calls.
		var message = new OrderCreatedMessage();

		// Act -- standard first.
		var standard = await selector.SelectTransportAsync(message, standardContext, CancellationToken.None);
		var premium = await selector.SelectTransportAsync(message, premiumContext, CancellationToken.None);

		// Assert
		standard.ShouldBe("ordinary");
		premium.ShouldBe("premium");
	}

	[Fact]
	public async Task HonourABaseTypeConditionalRuleAfterAnEarlierMessageTookTheUnconditionalRule()
	{
		// Arrange -- the conditional rule is registered against the base type, so it applies to the
		// derived message through IsAssignableFrom, and its outcome is still instance-dependent.
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderMessageBase>()
					.When(msg => msg.Amount > 1000).To("premium")
				.Route<PriorityOrderCreatedMessage>().To("ordinary")
				.Default("local");
		});
		var context = CreateContext();

		// Act
		var ordinary = await selector.SelectTransportAsync(
			new PriorityOrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);
		var premium = await selector.SelectTransportAsync(
			new PriorityOrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None);

		// Assert
		ordinary.ShouldBe("ordinary");
		premium.ShouldBe("premium");
	}

	[Fact]
	public async Task ReEvaluateTheConditionalRuleOnEveryCallOnceItAppliesToTheType()
	{
		// Liveness for the suppression: the cache must not swallow the predicate. Counting invocations
		// is the direct observation of "this type is re-evaluated", not a proxy for it.
		var predicateInvocations = 0;
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When(msg =>
					{
						predicateInvocations++;
						return msg.Amount > 1000;
					})
					.To("premium")
				.Route<OrderCreatedMessage>().To("ordinary")
				.Default("local");
		});
		var context = CreateContext();

		// Act
		for (var call = 0; call < 5; call++)
		{
			_ = await selector.SelectTransportAsync(
				new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);
		}

		// Assert
		predicateInvocations.ShouldBe(5);
	}

	[Fact]
	public async Task StillCacheAnUnconditionalRuleWhenNoConditionalRuleAppliesToTheType()
	{
		// Liveness for the caching that is retained: a type with no applicable predicate is
		// type-decidable, so the cache is still the right answer for it and must keep returning it.
		var selector = CreateSelector(builder =>
		{
			builder.Transport
				.Route<OrderCreatedMessage>()
					.When(msg => msg.Amount > 1000).To("premium")
				.Route<OrderCreatedMessage>().To("ordinary")
				.Route<PaymentProcessedMessage>().To("payments")
				.Default("local");
		});
		var context = CreateContext();

		// Act
		var first = await selector.SelectTransportAsync(
			new PaymentProcessedMessage(), context, CancellationToken.None);
		var second = await selector.SelectTransportAsync(
			new PaymentProcessedMessage(), context, CancellationToken.None);

		// Assert
		first.ShouldBe("payments");
		second.ShouldBe("payments");
	}

	[Fact]
	public async Task HonourConditionalRoutingThroughThePublicRoutingRegistration()
	{
		// The selector is registered as a singleton, so the cache is shared for the lifetime of the
		// host. This exercises the registered instance rather than a directly constructed one.
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UseRouting(routing =>
		{
			routing.Transport
				.Route<OrderCreatedMessage>()
					.When(msg => msg.Amount > 1000).To("premium")
				.Route<OrderCreatedMessage>().To("ordinary")
				.Default("local");
		});

		using var provider = services.BuildServiceProvider();
		var selector = provider.GetRequiredService<ITransportSelector>();
		var context = CreateContext();

		// Act -- ordinary first.
		var ordinary = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);
		var premium = await selector.SelectTransportAsync(
			new OrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None);

		// Assert
		selector.ShouldBeSameAs(provider.GetRequiredService<ITransportSelector>());
		ordinary.ShouldBe("ordinary");
		premium.ShouldBe("premium");
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

	private abstract class OrderMessageBase : IIntegrationEvent
	{
		public decimal Amount { get; init; }
	}

	private sealed class PriorityOrderCreatedMessage : OrderMessageBase;

	#endregion
}
