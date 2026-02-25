// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="SagaTimeout"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaTimeoutShould
{
	#region Constructor Tests

	[Fact]
	public void InitializeWithAllProperties()
	{
		// Arrange
		var dueAt = DateTime.UtcNow.AddMinutes(5);
		var scheduledAt = DateTime.UtcNow;
		var timeoutData = new byte[] { 1, 2, 3, 4 };

		// Act
		var timeout = new SagaTimeout(
			TimeoutId: "timeout-123",
			SagaId: "saga-456",
			SagaType: "OrderSaga",
			TimeoutType: "PaymentTimeout",
			TimeoutData: timeoutData,
			DueAt: dueAt,
			ScheduledAt: scheduledAt);

		// Assert
		timeout.TimeoutId.ShouldBe("timeout-123");
		timeout.SagaId.ShouldBe("saga-456");
		timeout.SagaType.ShouldBe("OrderSaga");
		timeout.TimeoutType.ShouldBe("PaymentTimeout");
		timeout.TimeoutData.ShouldBe(timeoutData);
		timeout.DueAt.ShouldBe(dueAt);
		timeout.ScheduledAt.ShouldBe(scheduledAt);
	}

	[Fact]
	public void AllowNullTimeoutData()
	{
		// Act
		var timeout = new SagaTimeout(
			TimeoutId: "timeout-123",
			SagaId: "saga-456",
			SagaType: "OrderSaga",
			TimeoutType: "PaymentTimeout",
			TimeoutData: null,
			DueAt: DateTime.UtcNow,
			ScheduledAt: DateTime.UtcNow);

		// Assert
		timeout.TimeoutData.ShouldBeNull();
	}

	#endregion Constructor Tests

	#region Record Equality Tests

	[Fact]
	public void BeEqual_WhenAllPropertiesAreEqual()
	{
		// Arrange
		var dueAt = DateTime.UtcNow.AddMinutes(5);
		var scheduledAt = DateTime.UtcNow;

		var timeout1 = new SagaTimeout(
			TimeoutId: "timeout-123",
			SagaId: "saga-456",
			SagaType: "OrderSaga",
			TimeoutType: "PaymentTimeout",
			TimeoutData: null,
			DueAt: dueAt,
			ScheduledAt: scheduledAt);

		var timeout2 = new SagaTimeout(
			TimeoutId: "timeout-123",
			SagaId: "saga-456",
			SagaType: "OrderSaga",
			TimeoutType: "PaymentTimeout",
			TimeoutData: null,
			DueAt: dueAt,
			ScheduledAt: scheduledAt);

		// Assert
		timeout1.ShouldBe(timeout2);
	}

	[Fact]
	public void NotBeEqual_WhenTimeoutIdDiffers()
	{
		// Arrange
		var dueAt = DateTime.UtcNow.AddMinutes(5);
		var scheduledAt = DateTime.UtcNow;

		var timeout1 = new SagaTimeout("timeout-1", "saga-456", "OrderSaga", "PaymentTimeout", null, dueAt, scheduledAt);
		var timeout2 = new SagaTimeout("timeout-2", "saga-456", "OrderSaga", "PaymentTimeout", null, dueAt, scheduledAt);

		// Assert
		timeout1.ShouldNotBe(timeout2);
	}

	[Fact]
	public void NotBeEqual_WhenSagaIdDiffers()
	{
		// Arrange
		var dueAt = DateTime.UtcNow.AddMinutes(5);
		var scheduledAt = DateTime.UtcNow;

		var timeout1 = new SagaTimeout("timeout-123", "saga-1", "OrderSaga", "PaymentTimeout", null, dueAt, scheduledAt);
		var timeout2 = new SagaTimeout("timeout-123", "saga-2", "OrderSaga", "PaymentTimeout", null, dueAt, scheduledAt);

		// Assert
		timeout1.ShouldNotBe(timeout2);
	}

	[Fact]
	public void NotBeEqual_WhenDueAtDiffers()
	{
		// Arrange
		var scheduledAt = DateTime.UtcNow;

		var timeout1 = new SagaTimeout("timeout-123", "saga-456", "OrderSaga", "PaymentTimeout", null, DateTime.UtcNow.AddMinutes(5), scheduledAt);
		var timeout2 = new SagaTimeout("timeout-123", "saga-456", "OrderSaga", "PaymentTimeout", null, DateTime.UtcNow.AddMinutes(10), scheduledAt);

		// Assert
		timeout1.ShouldNotBe(timeout2);
	}

	#endregion Record Equality Tests

	#region With Expression Tests

	[Fact]
	public void SupportWithExpression_ForDueAt()
	{
		// Arrange
		var original = new SagaTimeout(
			TimeoutId: "timeout-123",
			SagaId: "saga-456",
			SagaType: "OrderSaga",
			TimeoutType: "PaymentTimeout",
			TimeoutData: null,
			DueAt: DateTime.UtcNow.AddMinutes(5),
			ScheduledAt: DateTime.UtcNow);

		var newDueAt = DateTime.UtcNow.AddMinutes(15);

		// Act
		var modified = original with { DueAt = newDueAt };

		// Assert
		modified.DueAt.ShouldBe(newDueAt);
		modified.TimeoutId.ShouldBe(original.TimeoutId);
		modified.SagaId.ShouldBe(original.SagaId);
	}

	[Fact]
	public void SupportWithExpression_ForTimeoutData()
	{
		// Arrange
		var original = new SagaTimeout(
			TimeoutId: "timeout-123",
			SagaId: "saga-456",
			SagaType: "OrderSaga",
			TimeoutType: "PaymentTimeout",
			TimeoutData: null,
			DueAt: DateTime.UtcNow.AddMinutes(5),
			ScheduledAt: DateTime.UtcNow);

		var newData = new byte[] { 1, 2, 3 };

		// Act
		var modified = original with { TimeoutData = newData };

		// Assert
		modified.TimeoutData.ShouldBe(newData);
		modified.TimeoutId.ShouldBe(original.TimeoutId);
	}

	#endregion With Expression Tests

	#region Deconstruction Tests

	[Fact]
	public void SupportDeconstruction()
	{
		// Arrange
		var dueAt = DateTime.UtcNow.AddMinutes(5);
		var scheduledAt = DateTime.UtcNow;

		var timeout = new SagaTimeout(
			TimeoutId: "timeout-123",
			SagaId: "saga-456",
			SagaType: "OrderSaga",
			TimeoutType: "PaymentTimeout",
			TimeoutData: null,
			DueAt: dueAt,
			ScheduledAt: scheduledAt);

		// Act
		var (timeoutId, sagaId, sagaType, timeoutType, timeoutData, due, scheduled) = timeout;

		// Assert
		timeoutId.ShouldBe("timeout-123");
		sagaId.ShouldBe("saga-456");
		sagaType.ShouldBe("OrderSaga");
		timeoutType.ShouldBe("PaymentTimeout");
		timeoutData.ShouldBeNull();
		due.ShouldBe(dueAt);
		scheduled.ShouldBe(scheduledAt);
	}

	#endregion Deconstruction Tests
}
