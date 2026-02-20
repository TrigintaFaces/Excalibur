// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// CA2012: FakeItEasy's .Returns() stores ValueTask internally - this is expected for test setup
#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="ConfiguredEndpointRouter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfiguredEndpointRouterShould
{
	#region Constructor tests

	[Fact]
	public void ThrowOnNullConfiguration()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ConfiguredEndpointRouter(null!));
	}

	#endregion

	#region RouteToEndpointsAsync tests

	[Fact]
	public async Task ReturnEmptyEndpointsWhenNoRulesMatch()
	{
		// Arrange
		var router = CreateRouter(builder => { });
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var endpoints = await router.RouteToEndpointsAsync(message, context, CancellationToken.None);

		// Assert
		endpoints.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEndpointsForMatchingType()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("billing-service", "inventory-service");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var endpoints = await router.RouteToEndpointsAsync(message, context, CancellationToken.None);

		// Assert
		endpoints.Count.ShouldBe(2);
		endpoints.ShouldContain("billing-service");
		endpoints.ShouldContain("inventory-service");
	}

	[Fact]
	public async Task NotReturnEndpointsForNonMatchingType()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("billing-service");
		});
		var message = new PaymentProcessedMessage();
		var context = CreateContext();

		// Act
		var endpoints = await router.RouteToEndpointsAsync(message, context, CancellationToken.None);

		// Assert
		endpoints.ShouldBeEmpty();
	}

	[Fact]
	public async Task EvaluateConditionalRules()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>()
					.To("billing-service")
					.When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");
		});
		var context = CreateContext();

		// Act
		var highValueEndpoints = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None);
		var normalEndpoints = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);

		// Assert
		highValueEndpoints.Count.ShouldBe(2);
		highValueEndpoints.ShouldContain("billing-service");
		highValueEndpoints.ShouldContain("fraud-detection");

		normalEndpoints.Count.ShouldBe(1);
		normalEndpoints.ShouldContain("billing-service");
	}

	[Fact]
	public async Task EvaluateConditionalRulesWithContext()
	{
		// Arrange
		var premiumContext = A.Fake<IMessageContext>();
		A.CallTo(() => premiumContext.TenantId).Returns("premium");
		var standardContext = A.Fake<IMessageContext>();
		A.CallTo(() => standardContext.TenantId).Returns("standard");

		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>()
					.To("standard-service")
					.When((OrderCreatedMessage msg, IMessageContext ctx) => ctx.TenantId == "premium")
						.AlsoTo("vip-service");
		});
		var message = new OrderCreatedMessage();

		// Act
		var premiumEndpoints = await router.RouteToEndpointsAsync(message, premiumContext, CancellationToken.None);
		var standardEndpoints = await router.RouteToEndpointsAsync(message, standardContext, CancellationToken.None);

		// Assert
		premiumEndpoints.ShouldContain("vip-service");
		standardEndpoints.ShouldNotContain("vip-service");
	}

	[Fact]
	public async Task DeduplicateEndpoints()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>()
					.To("billing-service")
					.When(msg => true).AlsoTo("billing-service"); // same endpoint again
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var endpoints = await router.RouteToEndpointsAsync(message, context, CancellationToken.None);

		// Assert
		endpoints.Count(e => e == "billing-service").ShouldBe(1); // Distinct()
	}

	[Fact]
	public async Task UseFallbackWhenNoEndpointsMatch()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<PaymentProcessedMessage>().To("payment-service");
			builder.Fallback.To("dead-letter-queue");
		});
		var message = new OrderCreatedMessage(); // no rules for this type
		var context = CreateContext();

		// Act
		var endpoints = await router.RouteToEndpointsAsync(message, context, CancellationToken.None);

		// Assert
		endpoints.Count.ShouldBe(1);
		endpoints.ShouldContain("dead-letter-queue");
	}

	[Fact]
	public async Task NotUseFallbackWhenEndpointsMatch()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("billing-service");
			builder.Fallback.To("dead-letter-queue");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var endpoints = await router.RouteToEndpointsAsync(message, context, CancellationToken.None);

		// Assert
		endpoints.ShouldContain("billing-service");
		endpoints.ShouldNotContain("dead-letter-queue");
	}

	[Fact]
	public async Task CacheResultsForUnconditionalRules()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("billing-service");
		});
		var context = CreateContext();

		// Act - call twice
		var first = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage(), context, CancellationToken.None);
		var second = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage(), context, CancellationToken.None);

		// Assert
		first.ShouldBe(second); // same cached instance
	}

	[Fact]
	public async Task NotCacheResultsWhenConditionalRulesExist()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>()
					.To("billing-service")
					.When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");
		});
		var context = CreateContext();

		// Act - different conditions
		var highValue = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage { Amount = 5000 }, context, CancellationToken.None);
		var lowValue = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage { Amount = 100 }, context, CancellationToken.None);

		// Assert
		highValue.Count.ShouldBe(2);
		lowValue.Count.ShouldBe(1);
	}

	[Fact]
	public async Task HandleMultipleMessageTypesWithEndpoints()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>()
					.To("billing-service")
				.Route<PaymentProcessedMessage>()
					.To("accounting-service");
		});
		var context = CreateContext();

		// Act
		var orderEndpoints = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage(), context, CancellationToken.None);
		var paymentEndpoints = await router.RouteToEndpointsAsync(
			new PaymentProcessedMessage(), context, CancellationToken.None);

		// Assert
		orderEndpoints.ShouldContain("billing-service");
		orderEndpoints.ShouldNotContain("accounting-service");
		paymentEndpoints.ShouldContain("accounting-service");
		paymentEndpoints.ShouldNotContain("billing-service");
	}

	[Fact]
	public async Task HandleMultipleConditionalRulesForSameType()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>()
					.To("base-service")
					.When(msg => msg.Amount > 1000).AlsoTo("fraud-detection")
					.When(msg => msg.Amount > 10000).AlsoTo("compliance-service");
		});
		var context = CreateContext();

		// Act
		var veryHighValue = await router.RouteToEndpointsAsync(
			new OrderCreatedMessage { Amount = 50000 }, context, CancellationToken.None);

		// Assert - both conditional rules match
		veryHighValue.ShouldContain("base-service");
		veryHighValue.ShouldContain("fraud-detection");
		veryHighValue.ShouldContain("compliance-service");
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		// Arrange
		var router = CreateRouter(_ => { });
		var context = CreateContext();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await router.RouteToEndpointsAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		// Arrange
		var router = CreateRouter(_ => { });
		var message = new OrderCreatedMessage();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await router.RouteToEndpointsAsync(message, null!, CancellationToken.None));
	}

	#endregion

	#region CanRouteToEndpoint tests

	[Fact]
	public void ReturnTrueForConfiguredEndpoint()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("billing-service");
		});
		var message = new OrderCreatedMessage();

		// Act
		var canRoute = router.CanRouteToEndpoint(message, "billing-service");

		// Assert
		canRoute.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForUnconfiguredEndpoint()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("billing-service");
		});
		var message = new OrderCreatedMessage();

		// Act
		var canRoute = router.CanRouteToEndpoint(message, "unknown-service");

		// Assert
		canRoute.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForFallbackEndpoint()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Fallback.To("dead-letter-queue");
		});
		var message = new OrderCreatedMessage();

		// Act
		var canRoute = router.CanRouteToEndpoint(message, "dead-letter-queue");

		// Assert
		canRoute.ShouldBeTrue();
	}

	[Fact]
	public void MatchEndpointCaseInsensitively()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("Billing-Service");
		});
		var message = new OrderCreatedMessage();

		// Act
		var canRoute = router.CanRouteToEndpoint(message, "billing-service");

		// Assert
		canRoute.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullMessageForCanRouteToEndpoint()
	{
		// Arrange
		var router = CreateRouter(_ => { });

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => router.CanRouteToEndpoint(null!, "endpoint"));
	}

	[Fact]
	public void ThrowOnNullEndpointForCanRouteToEndpoint()
	{
		// Arrange
		var router = CreateRouter(_ => { });
		var message = new OrderCreatedMessage();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => router.CanRouteToEndpoint(message, null!));
	}

	[Fact]
	public void ThrowOnEmptyEndpointForCanRouteToEndpoint()
	{
		// Arrange
		var router = CreateRouter(_ => { });
		var message = new OrderCreatedMessage();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => router.CanRouteToEndpoint(message, ""));
	}

	[Fact]
	public void ReturnFalseWhenNoRulesForMessageType()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<PaymentProcessedMessage>().To("payment-service");
		});
		var message = new OrderCreatedMessage();

		// Act
		var canRoute = router.CanRouteToEndpoint(message, "payment-service");

		// Assert
		canRoute.ShouldBeFalse();
	}

	#endregion

	#region GetEndpointRoutes tests

	[Fact]
	public void ReturnRouteInfoForConfiguredEndpoints()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("billing-service", "inventory-service");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var routes = router.GetEndpointRoutes(message, context).ToList();

		// Assert
		routes.Count.ShouldBe(2);
		routes.ShouldContain(r => r.Endpoint == "billing-service");
		routes.ShouldContain(r => r.Endpoint == "inventory-service");
	}

	[Fact]
	public void IncludeRuleTypeMetadata()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>()
					.To("billing-service")
					.When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var routes = router.GetEndpointRoutes(message, context).ToList();

		// Assert
		var unconditional = routes.First(r => r.Endpoint == "billing-service");
		unconditional.Metadata["rule_type"].ShouldBe("unconditional");

		var conditional = routes.First(r => r.Endpoint == "fraud-detection");
		conditional.Metadata["rule_type"].ShouldBe("conditional");
	}

	[Fact]
	public void IncludeFallbackRoute()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Fallback.To("dead-letter-queue").WithReason("No rules");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var routes = router.GetEndpointRoutes(message, context).ToList();

		// Assert
		var fallback = routes.Single(r => r.Endpoint == "dead-letter-queue");
		fallback.Name.ShouldBe("fallback");
		fallback.Priority.ShouldBe(int.MaxValue);
		fallback.Metadata["rule_type"].ShouldBe("fallback");
		fallback.Metadata["is_fallback"].ShouldBe(true);
		fallback.Metadata["fallback_reason"].ShouldBe("No rules");
	}

	[Fact]
	public void NotIncludeFallbackReasonWhenNotConfigured()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Fallback.To("dead-letter-queue");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var routes = router.GetEndpointRoutes(message, context).ToList();

		// Assert
		var fallback = routes.Single(r => r.Endpoint == "dead-letter-queue");
		fallback.Metadata.ShouldNotContainKey("fallback_reason");
	}

	[Fact]
	public void AssignIncrementingPriorities()
	{
		// Arrange
		var router = CreateRouter(builder =>
		{
			builder.Endpoints
				.Route<OrderCreatedMessage>().To("service-a", "service-b")
				.When(msg => true).AlsoTo("service-c");
		});
		var message = new OrderCreatedMessage();
		var context = CreateContext();

		// Act
		var routes = router.GetEndpointRoutes(message, context).ToList();

		// Assert - priorities should increment per rule, not per endpoint
		var serviceA = routes.First(r => r.Endpoint == "service-a");
		var serviceB = routes.First(r => r.Endpoint == "service-b");
		serviceA.Priority.ShouldBe(serviceB.Priority); // same rule = same priority
	}

	[Fact]
	public void ThrowOnNullMessageForGetEndpointRoutes()
	{
		// Arrange
		var router = CreateRouter(_ => { });
		var context = CreateContext();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => router.GetEndpointRoutes(null!, context).ToList());
	}

	[Fact]
	public void ThrowOnNullContextForGetEndpointRoutes()
	{
		// Arrange
		var router = CreateRouter(_ => { });
		var message = new OrderCreatedMessage();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => router.GetEndpointRoutes(message, null!).ToList());
	}

	#endregion

	#region Helpers

	private static ConfiguredEndpointRouter CreateRouter(Action<RoutingBuilder> configure)
	{
		var builder = new RoutingBuilder();
		configure(builder);
		var config = new RoutingConfiguration(builder);
		return new ConfiguredEndpointRouter(config);
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	#endregion

	#region Test message types

	private sealed class OrderCreatedMessage : IDispatchMessage
	{
		public decimal Amount { get; init; }
	}

	private sealed class PaymentProcessedMessage : IDispatchMessage;

	#endregion
}
