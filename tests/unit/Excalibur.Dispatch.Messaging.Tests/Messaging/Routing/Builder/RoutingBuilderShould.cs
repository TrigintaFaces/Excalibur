// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="RoutingBuilder"/> and <see cref="IRoutingBuilder"/> interface.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingBuilderShould
{
	#region IRoutingBuilder properties

	[Fact]
	public void ExposeTransportBuilder()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Assert
		builder.Transport.ShouldNotBeNull();
	}

	[Fact]
	public void ExposeEndpointsBuilder()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Assert
		builder.Endpoints.ShouldNotBeNull();
	}

	[Fact]
	public void ExposeFallbackBuilder()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Assert
		builder.Fallback.ShouldNotBeNull();
	}

	#endregion

	#region Transport routing builder

	[Fact]
	public void RegisterTransportRule()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Transport.Route<TestMessage>().To("rabbitmq");

		// Assert
		var rules = builder.Transport.GetRules();
		rules.Count.ShouldBe(1);
		rules[0].MessageType.ShouldBe(typeof(TestMessage));
		rules[0].Transport.ShouldBe("rabbitmq");
		rules[0].Predicate.ShouldBeNull();
	}

	[Fact]
	public void RegisterMultipleTransportRules()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Transport
			.Route<TestMessage>().To("rabbitmq")
			.Route<AnotherTestMessage>().To("kafka");

		// Assert
		var rules = builder.Transport.GetRules();
		rules.Count.ShouldBe(2);
		rules[0].Transport.ShouldBe("rabbitmq");
		rules[1].Transport.ShouldBe("kafka");
	}

	[Fact]
	public void SetDefaultTransport()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Transport.Default("local");

		// Assert
		builder.Transport.DefaultTransport.ShouldBe("local");
	}

	[Fact]
	public void OverrideDefaultTransport()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Transport
			.Default("local")
			.Default("rabbitmq");

		// Assert
		builder.Transport.DefaultTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void DefaultTransportToNullWhenNotSet()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Assert
		builder.Transport.DefaultTransport.ShouldBeNull();
	}

	[Fact]
	public void ThrowOnNullTransportName()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Transport.Route<TestMessage>().To(null!));
	}

	[Fact]
	public void ThrowOnEmptyTransportName()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Transport.Route<TestMessage>().To(""));
	}

	[Fact]
	public void ThrowOnWhitespaceTransportName()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Transport.Route<TestMessage>().To("   "));
	}

	[Fact]
	public void ThrowOnNullDefaultTransport()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Transport.Default(null!));
	}

	[Fact]
	public void ThrowOnEmptyDefaultTransport()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Transport.Default(""));
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act - this should compile and not throw
		builder.Transport
			.Route<TestMessage>().To("rabbitmq")
			.Route<AnotherTestMessage>().To("kafka")
			.Default("local");

		// Assert
		builder.Transport.GetRules().Count.ShouldBe(2);
		builder.Transport.DefaultTransport.ShouldBe("local");
	}

	#endregion

	#region Conditional transport routing

	[Fact]
	public void RegisterConditionalTransportRule()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Transport
			.Route<TestMessage>().When(msg => msg.IsHighPriority).To("kafka");

		// Assert
		var rules = builder.Transport.GetRules();
		rules.Count.ShouldBe(1);
		rules[0].Predicate.ShouldNotBeNull();
		rules[0].Transport.ShouldBe("kafka");
	}

	[Fact]
	public void RegisterConditionalTransportRuleWithContext()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Transport
			.Route<TestMessage>().When((msg, ctx) => ctx.TenantId == "premium").To("kafka");

		// Assert
		var rules = builder.Transport.GetRules();
		rules.Count.ShouldBe(1);
		rules[0].Predicate.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullPredicate()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.Transport.Route<TestMessage>().When((Func<TestMessage, bool>)null!));
	}

	[Fact]
	public void ThrowOnNullContextPredicate()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.Transport.Route<TestMessage>().When((Func<TestMessage, IMessageContext, bool>)null!));
	}

	[Fact]
	public void MixConditionalAndUnconditionalTransportRules()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Transport
			.Route<TestMessage>().When(msg => msg.IsHighPriority).To("kafka")
			.Route<TestMessage>().To("rabbitmq")
			.Default("local");

		// Assert
		var rules = builder.Transport.GetRules();
		rules.Count.ShouldBe(2);
		rules[0].Predicate.ShouldNotBeNull();
		rules[1].Predicate.ShouldBeNull();
	}

	#endregion

	#region Endpoint routing builder

	[Fact]
	public void RegisterEndpointRule()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Endpoints
			.Route<TestMessage>().To("billing-service");

		// Assert
		var rules = builder.Endpoints.GetRules();
		rules.Count.ShouldBe(1);
		rules[0].MessageType.ShouldBe(typeof(TestMessage));
		rules[0].Endpoints.ShouldContain("billing-service");
		rules[0].Predicate.ShouldBeNull();
	}

	[Fact]
	public void RegisterMultipleEndpoints()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Endpoints
			.Route<TestMessage>().To("billing-service", "inventory-service", "analytics-service");

		// Assert
		var rules = builder.Endpoints.GetRules();
		rules.Count.ShouldBe(1);
		rules[0].Endpoints.Count.ShouldBe(3);
		rules[0].Endpoints.ShouldContain("billing-service");
		rules[0].Endpoints.ShouldContain("inventory-service");
		rules[0].Endpoints.ShouldContain("analytics-service");
	}

	[Fact]
	public void RegisterConditionalEndpointRule()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Endpoints
			.Route<TestMessage>()
				.To("billing-service")
				.When(msg => msg.IsHighPriority).AlsoTo("fraud-detection");

		// Assert
		var rules = builder.Endpoints.GetRules();
		rules.Count.ShouldBe(2);
		rules[0].Predicate.ShouldBeNull();
		rules[0].Endpoints.ShouldContain("billing-service");
		rules[1].Predicate.ShouldNotBeNull();
		rules[1].Endpoints.ShouldContain("fraud-detection");
	}

	[Fact]
	public void RegisterConditionalEndpointRuleWithContext()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Endpoints
			.Route<TestMessage>()
				.To("billing-service")
				.When((TestMessage msg, IMessageContext ctx) => ctx.TenantId == "premium").AlsoTo("vip-service");

		// Assert
		var rules = builder.Endpoints.GetRules();
		rules.Count.ShouldBe(2);
		rules[1].Predicate.ShouldNotBeNull();
		rules[1].Endpoints.ShouldContain("vip-service");
	}

	[Fact]
	public void ChainMultipleMessageTypesForEndpoints()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Endpoints
			.Route<TestMessage>()
				.To("billing-service")
			.Route<AnotherTestMessage>()
				.To("analytics-service");

		// Assert
		var rules = builder.Endpoints.GetRules();
		rules.Count.ShouldBe(2);
		rules[0].MessageType.ShouldBe(typeof(TestMessage));
		rules[1].MessageType.ShouldBe(typeof(AnotherTestMessage));
	}

	[Fact]
	public void ChainMultipleConditionalEndpoints()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Endpoints
			.Route<TestMessage>()
				.To("billing-service", "inventory-service")
				.When(msg => msg.IsHighPriority).AlsoTo("fraud-detection")
				.When(msg => msg.Amount > 10000).AlsoTo("compliance-service");

		// Assert
		var rules = builder.Endpoints.GetRules();
		rules.Count.ShouldBe(3); // 1 unconditional + 2 conditional
	}

	[Fact]
	public void ThrowOnNullEndpoints()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Endpoints.Route<TestMessage>().To(null!));
	}

	[Fact]
	public void ThrowOnEmptyEndpointsArray()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Endpoints.Route<TestMessage>().To());
	}

	[Fact]
	public void ThrowOnEndpointWithWhitespace()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Endpoints.Route<TestMessage>().To("valid", "   "));
	}

	[Fact]
	public void ThrowOnNullEndpointPredicate()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.Endpoints
				.Route<TestMessage>()
					.To("service")
					.When((Func<TestMessage, bool>)null!));
	}

	[Fact]
	public void ThrowOnNullEndpointContextPredicate()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.Endpoints
				.Route<TestMessage>()
					.To("service")
					.When((Func<TestMessage, IMessageContext, bool>)null!));
	}

	[Fact]
	public void ThrowOnNullAlsoToEndpoints()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Endpoints
				.Route<TestMessage>()
					.To("service")
					.When(msg => true).AlsoTo(null!));
	}

	[Fact]
	public void ThrowOnEmptyAlsoToEndpoints()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Endpoints
				.Route<TestMessage>()
					.To("service")
					.When(msg => true).AlsoTo());
	}

	[Fact]
	public void GetRulesFromChainBuilder()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		var chain = builder.Endpoints
			.Route<TestMessage>().To("service");

		// Assert
		chain.GetRules().Count.ShouldBe(1);
	}

	#endregion

	#region Fallback routing builder

	[Fact]
	public void SetFallbackEndpoint()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Fallback.To("dead-letter-queue");

		// Assert
		builder.Fallback.Endpoint.ShouldBe("dead-letter-queue");
	}

	[Fact]
	public void SetFallbackReason()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Fallback
			.To("dead-letter-queue")
			.WithReason("No matching routing rules");

		// Assert
		builder.Fallback.Endpoint.ShouldBe("dead-letter-queue");
		builder.Fallback.Reason.ShouldBe("No matching routing rules");
	}

	[Fact]
	public void DefaultFallbackEndpointToNull()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Assert
		builder.Fallback.Endpoint.ShouldBeNull();
	}

	[Fact]
	public void DefaultFallbackReasonToNull()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Assert
		builder.Fallback.Reason.ShouldBeNull();
	}

	[Fact]
	public void ThrowOnNullFallbackEndpoint()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Fallback.To(null!));
	}

	[Fact]
	public void ThrowOnEmptyFallbackEndpoint()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Fallback.To(""));
	}

	[Fact]
	public void ThrowOnNullFallbackReason()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Fallback.WithReason(null!));
	}

	[Fact]
	public void ThrowOnEmptyFallbackReason()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.Fallback.WithReason(""));
	}

	[Fact]
	public void SupportFluentFallbackChaining()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		builder.Fallback
			.To("dead-letter-queue")
			.WithReason("No matching rules");

		// Assert
		builder.Fallback.Endpoint.ShouldBe("dead-letter-queue");
		builder.Fallback.Reason.ShouldBe("No matching rules");
	}

	#endregion

	#region Full API integration

	[Fact]
	public void SupportFullFluentAPI()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act - exercising the full API as documented
		builder.Transport
			.Route<TestMessage>().To("rabbitmq")
			.Route<AnotherTestMessage>().To("kafka")
			.Default("local");

		builder.Endpoints
			.Route<TestMessage>()
				.To("billing-service", "inventory-service")
				.When(msg => msg.IsHighPriority).AlsoTo("fraud-detection")
			.Route<AnotherTestMessage>()
				.To("analytics-service");

		builder.Fallback.To("dead-letter-queue").WithReason("Unmatched message");

		// Assert
		builder.Transport.GetRules().Count.ShouldBe(2);
		builder.Transport.DefaultTransport.ShouldBe("local");
		builder.Endpoints.GetRules().Count.ShouldBe(3);
		builder.Fallback.Endpoint.ShouldBe("dead-letter-queue");
		builder.Fallback.Reason.ShouldBe("Unmatched message");
	}

	#endregion

	#region AD-520.8: Transport routing IIntegrationEvent constraint

	/// <summary>
	/// Verifies that transport routing test event types implement <see cref="IIntegrationEvent"/>.
	/// The generic constraint <c>where TEvent : IIntegrationEvent</c> on
	/// <c>ITransportRoutingBuilder.Route&lt;TEvent&gt;()</c> is enforced at compile time.
	/// </summary>
	[Fact]
	public void RequireIIntegrationEventForTransportRouting()
	{
		// The constraint is compile-time enforced. If TestMessage did not implement
		// IIntegrationEvent, the following line would produce CS0311.
		var builder = new RoutingBuilder();
		builder.Transport.Route<TestMessage>().To("rabbitmq");

		// Verify the test types actually implement IIntegrationEvent
		typeof(IIntegrationEvent).IsAssignableFrom(typeof(TestMessage)).ShouldBeTrue();
		typeof(IIntegrationEvent).IsAssignableFrom(typeof(AnotherTestMessage)).ShouldBeTrue();
	}

	// AD-520.8: The following would NOT compile (CS0311) because IDispatchMessage
	// does not satisfy the IIntegrationEvent constraint:
	//
	// #if false
	// private sealed class CommandMessage : IDispatchMessage;
	// builder.Transport.Route<CommandMessage>().To("rabbitmq"); // CS0311
	// #endif

	#endregion

	#region Test message types

	private sealed class TestMessage : IIntegrationEvent
	{
		public bool IsHighPriority { get; init; }
		public decimal Amount { get; init; }
	}

	private sealed class AnotherTestMessage : IIntegrationEvent;

	#endregion
}
