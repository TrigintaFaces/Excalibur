// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="TransportRoutingBuilderExtensions.RouteAll"/> (Sprint 656 Q.3 / N.10).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportRoutingBuilderExtensionsShould
{
	#region Null guards

	[Fact]
	public void RouteAll_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		ITransportRoutingBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => builder.RouteAll("rabbitmq", typeof(TestIntegrationEvent)));
	}

	[Fact]
	public void RouteAll_ThrowsArgumentException_WhenTransportNameIsNull()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => routing.Transport.RouteAll(null!, typeof(TestIntegrationEvent)));
	}

	[Fact]
	public void RouteAll_ThrowsArgumentException_WhenTransportNameIsEmpty()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => routing.Transport.RouteAll("", typeof(TestIntegrationEvent)));
	}

	[Fact]
	public void RouteAll_ThrowsArgumentException_WhenTransportNameIsWhitespace()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => routing.Transport.RouteAll("   ", typeof(TestIntegrationEvent)));
	}

	[Fact]
	public void RouteAll_ThrowsArgumentNullException_WhenMessageTypesArrayIsNull()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => routing.Transport.RouteAll("rabbitmq", null!));
	}

	[Fact]
	public void RouteAll_ThrowsArgumentNullException_WhenAnyTypeIsNull()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => routing.Transport.RouteAll("rabbitmq", typeof(TestIntegrationEvent), null!));
	}

	#endregion

	#region Type validation

	[Fact]
	public void RouteAll_ThrowsArgumentException_WhenTypeDoesNotImplementIIntegrationEvent()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(
			() => routing.Transport.RouteAll("rabbitmq", typeof(NonIntegrationEvent)));

		ex.Message.ShouldContain("NonIntegrationEvent");
		ex.Message.ShouldContain("IIntegrationEvent");
	}

	[Fact]
	public void RouteAll_ThrowsArgumentException_WhenMixedValidAndInvalidTypes()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act & Assert -- should throw on the invalid type
		_ = Should.Throw<ArgumentException>(
			() => routing.Transport.RouteAll("rabbitmq",
				typeof(TestIntegrationEvent),
				typeof(NonIntegrationEvent)));
	}

	#endregion

	#region Happy path

	[Fact]
	public void RouteAll_RegistersSingleType()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act
		routing.Transport.RouteAll("rabbitmq", typeof(TestIntegrationEvent));

		// Assert
		var rules = routing.Transport.GetRules();
		rules.Count.ShouldBe(1);
		rules[0].MessageType.ShouldBe(typeof(TestIntegrationEvent));
		rules[0].Transport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void RouteAll_RegistersMultipleTypes()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act
		routing.Transport.RouteAll("kafka",
			typeof(TestIntegrationEvent),
			typeof(AnotherIntegrationEvent));

		// Assert
		var rules = routing.Transport.GetRules();
		rules.Count.ShouldBe(2);
		rules[0].MessageType.ShouldBe(typeof(TestIntegrationEvent));
		rules[0].Transport.ShouldBe("kafka");
		rules[1].MessageType.ShouldBe(typeof(AnotherIntegrationEvent));
		rules[1].Transport.ShouldBe("kafka");
	}

	[Fact]
	public void RouteAll_ReturnsBuilder_ForFluentChaining()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act
		var result = routing.Transport.RouteAll("rabbitmq", typeof(TestIntegrationEvent));

		// Assert
		result.ShouldBe(routing.Transport);
	}

	[Fact]
	public void RouteAll_IsNoOp_WhenEmptyArray()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act
		routing.Transport.RouteAll("rabbitmq");

		// Assert
		var rules = routing.Transport.GetRules();
		rules.Count.ShouldBe(0);
	}

	[Fact]
	public void RouteAll_CanChainWithManualRouting()
	{
		// Arrange
		var routing = new RoutingBuilder();

		// Act -- mix RouteAll with manual Route<T>().To()
		routing.Transport
			.RouteAll("rabbitmq", typeof(TestIntegrationEvent))
			.Route<AnotherIntegrationEvent>().To("kafka");

		// Assert
		var rules = routing.Transport.GetRules();
		rules.Count.ShouldBe(2);
		rules[0].Transport.ShouldBe("rabbitmq");
		rules[1].Transport.ShouldBe("kafka");
	}

	#endregion

	#region Test types

	private sealed class TestIntegrationEvent : IIntegrationEvent;

	private sealed class AnotherIntegrationEvent : IIntegrationEvent;

	private sealed class NonIntegrationEvent;

	#endregion
}
