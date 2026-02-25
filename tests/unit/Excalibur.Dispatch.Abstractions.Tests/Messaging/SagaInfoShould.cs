// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Abstractions.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="SagaInfo"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaInfoShould
{
	[Fact]
	public void Constructor_SetsSagaTypeAndStateType()
	{
		// Act
		var info = new SagaInfo(typeof(string), typeof(int));

		// Assert
		info.SagaType.ShouldBe(typeof(string));
		info.StateType.ShouldBe(typeof(int));
	}

	[Fact]
	public void StartsWith_RegistersStartEvent()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));

		// Act
		var result = info.StartsWith<OrderCreatedEvent>();

		// Assert
		info.IsStartEvent(typeof(OrderCreatedEvent)).ShouldBeTrue();
		result.ShouldBeSameAs(info); // Fluent chaining
	}

	[Fact]
	public void StartsWith_AlsoRegistersAsHandledEvent()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));

		// Act
		info.StartsWith<OrderCreatedEvent>();

		// Assert
		info.HandlesEvent(typeof(OrderCreatedEvent)).ShouldBeTrue();
	}

	[Fact]
	public void Handles_RegistersHandledEvent()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));

		// Act
		var result = info.Handles<PaymentReceivedEvent>();

		// Assert
		info.HandlesEvent(typeof(PaymentReceivedEvent)).ShouldBeTrue();
		result.ShouldBeSameAs(info); // Fluent chaining
	}

	[Fact]
	public void Handles_DoesNotRegisterAsStartEvent()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));

		// Act
		info.Handles<PaymentReceivedEvent>();

		// Assert
		info.IsStartEvent(typeof(PaymentReceivedEvent)).ShouldBeFalse();
	}

	[Fact]
	public void IsStartEvent_ReturnsFalse_ForUnregisteredType()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));

		// Act & Assert
		info.IsStartEvent(typeof(string)).ShouldBeFalse();
	}

	[Fact]
	public void HandlesEvent_ReturnsFalse_ForUnregisteredType()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));

		// Act & Assert
		info.HandlesEvent(typeof(string)).ShouldBeFalse();
	}

	[Fact]
	public void GetHandledEvents_ReturnsAllRegisteredEvents()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));
		info.StartsWith<OrderCreatedEvent>();
		info.Handles<PaymentReceivedEvent>();
		info.Handles<ShippingCompletedEvent>();

		// Act
		var handledEvents = info.GetHandledEvents().ToList();

		// Assert
		handledEvents.Count.ShouldBe(3);
		handledEvents.ShouldContain(typeof(OrderCreatedEvent));
		handledEvents.ShouldContain(typeof(PaymentReceivedEvent));
		handledEvents.ShouldContain(typeof(ShippingCompletedEvent));
	}

	[Fact]
	public void GetHandledEvents_ReturnsEmpty_WhenNoEventsRegistered()
	{
		// Arrange
		var info = new SagaInfo(typeof(string), typeof(int));

		// Act
		var handledEvents = info.GetHandledEvents().ToList();

		// Assert
		handledEvents.ShouldBeEmpty();
	}

	[Fact]
	public void FluentChaining_Works()
	{
		// Arrange & Act
		var info = new SagaInfo(typeof(string), typeof(int))
			.StartsWith<OrderCreatedEvent>()
			.Handles<PaymentReceivedEvent>()
			.Handles<ShippingCompletedEvent>();

		// Assert
		info.GetHandledEvents().Count().ShouldBe(3);
	}

	// Test event types
	private sealed record OrderCreatedEvent;
	private sealed record PaymentReceivedEvent;
	private sealed record ShippingCompletedEvent;
}
