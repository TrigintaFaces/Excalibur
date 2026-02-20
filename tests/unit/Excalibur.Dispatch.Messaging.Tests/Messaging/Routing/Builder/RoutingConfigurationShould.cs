// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="RoutingConfiguration"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingConfigurationShould
{
	[Fact]
	public void CaptureTransportRules()
	{
		// Arrange
		var builder = new RoutingBuilder();
		builder.Transport.Route<TestMessage>().To("rabbitmq");

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.TransportRules.Count.ShouldBe(1);
		config.TransportRules[0].Transport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void CaptureEndpointRules()
	{
		// Arrange
		var builder = new RoutingBuilder();
		builder.Endpoints.Route<TestMessage>().To("billing-service");

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.EndpointRules.Count.ShouldBe(1);
		config.EndpointRules[0].Endpoints.ShouldContain("billing-service");
	}

	[Fact]
	public void DefaultTransportToLocalWhenNotConfigured()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.DefaultTransport.ShouldBe("local");
	}

	[Fact]
	public void UseConfiguredDefaultTransport()
	{
		// Arrange
		var builder = new RoutingBuilder();
		builder.Transport.Default("rabbitmq");

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.DefaultTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void CaptureFallbackEndpoint()
	{
		// Arrange
		var builder = new RoutingBuilder();
		builder.Fallback.To("dead-letter-queue");

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.FallbackEndpoint.ShouldBe("dead-letter-queue");
	}

	[Fact]
	public void CaptureFallbackReason()
	{
		// Arrange
		var builder = new RoutingBuilder();
		builder.Fallback.To("dlq").WithReason("No matching rules");

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.FallbackReason.ShouldBe("No matching rules");
	}

	[Fact]
	public void DefaultFallbackToNull()
	{
		// Arrange
		var builder = new RoutingBuilder();

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.FallbackEndpoint.ShouldBeNull();
		config.FallbackReason.ShouldBeNull();
	}

	[Fact]
	public void ThrowOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new RoutingConfiguration(null!));
	}

	[Fact]
	public void CaptureComplexConfiguration()
	{
		// Arrange
		var builder = new RoutingBuilder();
		builder.Transport
			.Route<TestMessage>().To("rabbitmq")
			.Route<AnotherMessage>().To("kafka")
			.Default("local");
		builder.Endpoints
			.Route<TestMessage>().To("billing", "inventory")
			.When(m => m.IsHighPriority).AlsoTo("fraud");
		builder.Fallback.To("dlq").WithReason("Unmatched");

		// Act
		var config = new RoutingConfiguration(builder);

		// Assert
		config.TransportRules.Count.ShouldBe(2);
		config.EndpointRules.Count.ShouldBe(2);
		config.DefaultTransport.ShouldBe("local");
		config.FallbackEndpoint.ShouldBe("dlq");
		config.FallbackReason.ShouldBe("Unmatched");
	}

	private sealed class TestMessage : IIntegrationEvent
	{
		public bool IsHighPriority { get; init; }
	}

	private sealed class AnotherMessage : IIntegrationEvent;
}
