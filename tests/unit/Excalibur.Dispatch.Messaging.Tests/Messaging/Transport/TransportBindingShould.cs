// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="TransportBinding"/>.
/// </summary>
/// <remarks>
/// Tests the transport binding implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportBindingShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act
		var binding = new TransportBinding(
			"test-binding",
			adapter,
			"orders/*");

		// Assert
		_ = binding.ShouldNotBeNull();
		binding.Name.ShouldBe("test-binding");
		binding.TransportAdapter.ShouldBe(adapter);
		binding.EndpointPattern.ShouldBe("orders/*");
		binding.PipelineProfile.ShouldBeNull();
		binding.AcceptedMessageKinds.ShouldBe(MessageKinds.All);
		binding.Priority.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithAllParameters_CreatesInstanceWithAllProperties()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var profile = A.Fake<IPipelineProfile>();

		// Act
		var binding = new TransportBinding(
			"test-binding",
			adapter,
			"events/*",
			profile,
			MessageKinds.Event,
			10);

		// Assert
		binding.Name.ShouldBe("test-binding");
		binding.TransportAdapter.ShouldBe(adapter);
		binding.EndpointPattern.ShouldBe("events/*");
		binding.PipelineProfile.ShouldBe(profile);
		binding.AcceptedMessageKinds.ShouldBe(MessageKinds.Event);
		binding.Priority.ShouldBe(10);
	}

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new TransportBinding(null!, adapter, "pattern/*"));
	}

	[Fact]
	public void Constructor_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new TransportBinding(string.Empty, adapter, "pattern/*"));
	}

	[Fact]
	public void Constructor_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new TransportBinding("   ", adapter, "pattern/*"));
	}

	[Fact]
	public void Constructor_WithNullAdapter_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransportBinding("binding", null!, "pattern/*"));
	}

	[Fact]
	public void Constructor_WithNullEndpointPattern_ThrowsArgumentException()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new TransportBinding("binding", adapter, null!));
	}

	[Fact]
	public void Constructor_WithEmptyEndpointPattern_ThrowsArgumentException()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new TransportBinding("binding", adapter, string.Empty));
	}

	#endregion

	#region Matches Method Tests - Exact Match

	[Fact]
	public void Matches_ExactMatch_ReturnsTrue()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "orders/create");

		// Act & Assert
		binding.Matches("orders/create").ShouldBeTrue();
	}

	[Fact]
	public void Matches_ExactMatch_CaseInsensitive()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "Orders/Create");

		// Act & Assert
		binding.Matches("orders/create").ShouldBeTrue();
		binding.Matches("ORDERS/CREATE").ShouldBeTrue();
	}

	[Fact]
	public void Matches_ExactMatch_DifferentEndpoint_ReturnsFalse()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "orders/create");

		// Act & Assert
		binding.Matches("orders/delete").ShouldBeFalse();
	}

	#endregion

	#region Matches Method Tests - Wildcard Patterns

	[Fact]
	public void Matches_StarWildcard_MatchesSingleSegment()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "orders/*");

		// Act & Assert
		binding.Matches("orders/create").ShouldBeTrue();
		binding.Matches("orders/delete").ShouldBeTrue();
		binding.Matches("orders/update").ShouldBeTrue();
	}

	[Fact]
	public void Matches_StarWildcard_MatchesMultipleSegments()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "orders/*");

		// Act & Assert
		binding.Matches("orders/create/item").ShouldBeTrue();
		binding.Matches("orders/v1/create").ShouldBeTrue();
	}

	[Fact]
	public void Matches_QuestionMarkWildcard_MatchesSingleCharacter()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "order?");

		// Act & Assert
		binding.Matches("orders").ShouldBeTrue();
		binding.Matches("order1").ShouldBeTrue();
		binding.Matches("orderX").ShouldBeTrue();
	}

	[Fact]
	public void Matches_QuestionMarkWildcard_DoesNotMatchMultipleCharacters()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "order?");

		// Act & Assert
		binding.Matches("order").ShouldBeFalse(); // Missing character
		binding.Matches("orders123").ShouldBeFalse(); // Too many characters
	}

	[Fact]
	public void Matches_CombinedWildcards_Works()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "v?/orders/*");

		// Act & Assert
		binding.Matches("v1/orders/create").ShouldBeTrue();
		binding.Matches("v2/orders/delete").ShouldBeTrue();
		binding.Matches("v1/orders/items/list").ShouldBeTrue();
	}

	[Fact]
	public void Matches_PrefixWildcard_Works()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding("binding", adapter, "*/orders");

		// Act & Assert
		binding.Matches("api/orders").ShouldBeTrue();
		binding.Matches("v1/api/orders").ShouldBeTrue();
	}

	#endregion

	#region Property Tests

	[Theory]
	[InlineData(MessageKinds.None)]
	[InlineData(MessageKinds.Action)]
	[InlineData(MessageKinds.Event)]
	[InlineData(MessageKinds.All)]
	public void AcceptedMessageKinds_CanBeSetToVariousKinds(MessageKinds kinds)
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act
		var binding = new TransportBinding("binding", adapter, "pattern", acceptedMessageKinds: kinds);

		// Assert
		binding.AcceptedMessageKinds.ShouldBe(kinds);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	public void Priority_CanBeSetToVariousValues(int priority)
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();

		// Act
		var binding = new TransportBinding("binding", adapter, "pattern", priority: priority);

		// Assert
		binding.Priority.ShouldBe(priority);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void EventBusBinding_MatchesAllEvents()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding(
			"event-bus",
			adapter,
			"events/*",
			acceptedMessageKinds: MessageKinds.Event,
			priority: 10);

		// Act & Assert
		binding.Matches("events/order-created").ShouldBeTrue();
		binding.Matches("events/payment-processed").ShouldBeTrue();
		binding.AcceptedMessageKinds.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void CommandBinding_MatchesSpecificCommand()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding(
			"orders-commands",
			adapter,
			"commands/orders/*",
			acceptedMessageKinds: MessageKinds.Action,
			priority: 5);

		// Act & Assert
		binding.Matches("commands/orders/create").ShouldBeTrue();
		binding.Matches("commands/orders/cancel").ShouldBeTrue();
		binding.Matches("commands/payments/process").ShouldBeFalse();
	}

	[Fact]
	public void VersionedApiBinding_MatchesVersionPattern()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var binding = new TransportBinding(
			"versioned-api",
			adapter,
			"api/v?/*");

		// Act & Assert
		binding.Matches("api/v1/orders").ShouldBeTrue();
		binding.Matches("api/v2/customers").ShouldBeTrue();
		binding.Matches("api/v10/orders").ShouldBeFalse(); // v10 is two characters
	}

	#endregion
}
