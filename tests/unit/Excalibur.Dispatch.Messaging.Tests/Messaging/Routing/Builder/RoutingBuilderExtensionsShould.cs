// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="RoutingBuilderExtensions"/> (UseRouting DI registration).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingBuilderExtensionsShould
{
	#region UseRouting validation tests

	[Fact]
	public void ThrowOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => RoutingBuilderExtensions.UseRouting(null!, _ => { }));
	}

	[Fact]
	public void ThrowOnNullConfigure()
	{
		// Arrange
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.UseRouting(null!));
	}

	#endregion

	#region DI registration tests

	[Fact]
	public void RegisterTransportSelector()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.UseRouting(routing =>
		{
			routing.Transport.Default("local");
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var selector = sp.GetService<ITransportSelector>();
		selector.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterEndpointRouter()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.UseRouting(routing =>
		{
			routing.Transport.Default("local");
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var router = sp.GetService<IEndpointRouter>();
		router.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterDispatchRouter()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.UseRouting(routing =>
		{
			routing.Transport.Default("local");
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var router = sp.GetService<IDispatchRouter>();
		router.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseRouting(_ => { });

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void NotOverrideExistingRegistrations()
	{
		// Arrange
		var services = new ServiceCollection();
		var customSelector = A.Fake<ITransportSelector>();
		services.AddSingleton(customSelector);

		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.UseRouting(routing =>
		{
			routing.Transport.Default("local");
		});

		// Assert - TryAddSingleton should not override existing
		var sp = services.BuildServiceProvider();
		var resolvedSelector = sp.GetService<ITransportSelector>();
		resolvedSelector.ShouldBe(customSelector);
	}

	[Fact]
	public void RegisterSingletonServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.UseRouting(routing =>
		{
			routing.Transport.Default("local");
		});

		// Assert - all services should be singletons
		var sp = services.BuildServiceProvider();
		var router1 = sp.GetService<IDispatchRouter>();
		var router2 = sp.GetService<IDispatchRouter>();
		router1.ShouldBe(router2); // same instance = singleton
	}

	#endregion

	#region Integration tests with full routing pipeline

	[Fact]
	public async Task ResolveWorkingTransportSelector()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UseRouting(routing =>
		{
			routing.Transport
				.Route<TestOrderMessage>().To("rabbitmq")
				.Default("local");
		});

		var sp = services.BuildServiceProvider();
		var selector = sp.GetRequiredService<ITransportSelector>();
		var context = A.Fake<IMessageContext>();

		// Act
		var transport = await selector.SelectTransportAsync(
			new TestOrderMessage(), context, CancellationToken.None);

		// Assert
		transport.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task ResolveWorkingEndpointRouter()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UseRouting(routing =>
		{
			routing.Endpoints
				.Route<TestOrderMessage>().To("billing-service", "inventory-service");
		});

		var sp = services.BuildServiceProvider();
		var router = sp.GetRequiredService<IEndpointRouter>();
		var context = A.Fake<IMessageContext>();

		// Act
		var endpoints = await router.RouteToEndpointsAsync(
			new TestOrderMessage(), context, CancellationToken.None);

		// Assert
		endpoints.Count.ShouldBe(2);
		endpoints.ShouldContain("billing-service");
	}

	[Fact]
	public async Task ResolveWorkingDispatchRouter()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UseRouting(routing =>
		{
			routing.Transport
				.Route<TestOrderMessage>().To("rabbitmq")
				.Default("local");
			routing.Endpoints
				.Route<TestOrderMessage>().To("billing-service");
		});

		var sp = services.BuildServiceProvider();
		var router = sp.GetRequiredService<IDispatchRouter>();
		var context = A.Fake<IMessageContext>();

		// Act
		var decision = await router.RouteAsync(
			new TestOrderMessage(), context, CancellationToken.None);

		// Assert
		decision.IsSuccess.ShouldBeTrue();
		decision.Transport.ShouldBe("rabbitmq");
		decision.Endpoints.ShouldContain("billing-service");
	}

	[Fact]
	public async Task SupportFullRoutingScenario()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UseRouting(routing =>
		{
			routing.Transport
				.Route<TestOrderMessage>().To("rabbitmq")
				.Route<TestPaymentMessage>().To("kafka")
				.Default("local");

			routing.Endpoints
				.Route<TestOrderMessage>()
					.To("billing-service", "inventory-service")
					.When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");

			routing.Fallback.To("dead-letter-queue");
		});

		var sp = services.BuildServiceProvider();
		var router = sp.GetRequiredService<IDispatchRouter>();
		var context = A.Fake<IMessageContext>();

		// Act - high value order
		var highValueDecision = await router.RouteAsync(
			new TestOrderMessage { Amount = 5000 }, context, CancellationToken.None);

		// Assert
		highValueDecision.IsSuccess.ShouldBeTrue();
		highValueDecision.Transport.ShouldBe("rabbitmq");
		highValueDecision.Endpoints.ShouldContain("billing-service");
		highValueDecision.Endpoints.ShouldContain("inventory-service");
		highValueDecision.Endpoints.ShouldContain("fraud-detection");

		// Act - low value order
		var lowValueDecision = await router.RouteAsync(
			new TestOrderMessage { Amount = 50 }, context, CancellationToken.None);

		// Assert
		lowValueDecision.Endpoints.ShouldContain("billing-service");
		lowValueDecision.Endpoints.ShouldContain("inventory-service");
		lowValueDecision.Endpoints.ShouldNotContain("fraud-detection");
	}

	#endregion

	#region Test message types

	private sealed class TestOrderMessage : IIntegrationEvent
	{
		public decimal Amount { get; init; }
	}

	private sealed class TestPaymentMessage : IIntegrationEvent;

	#endregion
}
