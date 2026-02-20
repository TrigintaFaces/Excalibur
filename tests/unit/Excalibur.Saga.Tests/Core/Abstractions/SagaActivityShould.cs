// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="SagaActivity"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaActivityShould
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange & Act
		var activity = new SagaActivity();

		// Assert
		activity.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveEmptyMessageByDefault()
	{
		// Arrange & Act
		var activity = new SagaActivity();

		// Assert
		activity.Message.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullDetailsByDefault()
	{
		// Arrange & Act
		var activity = new SagaActivity();

		// Assert
		activity.Details.ShouldBeNull();
	}

	[Fact]
	public void HaveNullStepNameByDefault()
	{
		// Arrange & Act
		var activity = new SagaActivity();

		// Assert
		activity.StepName.ShouldBeNull();
	}

	#endregion Default Values Tests

	#region Property Initialization Tests

	[Fact]
	public void AllowTimestampToBeInitialized()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var activity = new SagaActivity { Timestamp = timestamp };

		// Assert
		activity.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowMessageToBeInitialized()
	{
		// Arrange & Act
		var activity = new SagaActivity { Message = "Order processed successfully" };

		// Assert
		activity.Message.ShouldBe("Order processed successfully");
	}

	[Fact]
	public void AllowDetailsToBeInitialized_WithPrimitiveType()
	{
		// Arrange & Act
		var activity = new SagaActivity { Details = 42 };

		// Assert
		activity.Details.ShouldBe(42);
	}

	[Fact]
	public void AllowDetailsToBeInitialized_WithComplexType()
	{
		// Arrange
		var details = new { OrderId = "order-123", Amount = 99.99m };

		// Act
		var activity = new SagaActivity { Details = details };

		// Assert
		activity.Details.ShouldNotBeNull();
		var detailsObj = (dynamic)activity.Details;
		((string)detailsObj.OrderId).ShouldBe("order-123");
	}

	[Fact]
	public void AllowStepNameToBeInitialized()
	{
		// Arrange & Act
		var activity = new SagaActivity { StepName = "ValidateOrder" };

		// Assert
		activity.StepName.ShouldBe("ValidateOrder");
	}

	#endregion Property Initialization Tests

	#region Comprehensive Activity Tests

	[Fact]
	public void CreateCompleteActivity()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var details = new { TransactionId = "txn-456" };

		// Act
		var activity = new SagaActivity
		{
			Timestamp = timestamp,
			Message = "Payment processed",
			Details = details,
			StepName = "ProcessPayment",
		};

		// Assert
		activity.Timestamp.ShouldBe(timestamp);
		activity.Message.ShouldBe("Payment processed");
		activity.StepName.ShouldBe("ProcessPayment");
		activity.Details.ShouldNotBeNull();
	}

	[Fact]
	public void CreateActivityWithoutOptionalFields()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var activity = new SagaActivity
		{
			Timestamp = timestamp,
			Message = "Saga started",
		};

		// Assert
		activity.Timestamp.ShouldBe(timestamp);
		activity.Message.ShouldBe("Saga started");
		activity.Details.ShouldBeNull();
		activity.StepName.ShouldBeNull();
	}

	#endregion Comprehensive Activity Tests
}
