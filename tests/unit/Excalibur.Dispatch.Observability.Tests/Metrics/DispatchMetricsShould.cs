// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="DispatchMetrics"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class DispatchMetricsShould : IDisposable
{
	private DispatchMetrics? _metrics;

	public void Dispose()
	{
		_metrics?.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void CreateMeterOnConstruction()
	{
		// Arrange & Act
		_metrics = new DispatchMetrics();

		// Assert
		_metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void UseMeterName()
	{
		// Arrange & Act
		_metrics = new DispatchMetrics();

		// Assert
		_metrics.Meter.Name.ShouldBe(DispatchMetrics.MeterName);
	}

	[Fact]
	public void HaveCorrectMeterNameConstant()
	{
		// Assert
		DispatchMetrics.MeterName.ShouldBe("Excalibur.Dispatch.Core");
	}

	#endregion

	#region RecordMessageProcessed Tests

	[Fact]
	public void RecordMessageProcessed_WithBasicParameters()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessageProcessed("TestMessage", "TestHandler");
	}

	[Fact]
	public void RecordMessageProcessed_WithAdditionalTags()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessageProcessed(
			"TestMessage",
			"TestHandler",
			("custom_tag", "value1"),
			("another_tag", 42));
	}

	[Fact]
	public void RecordMessageProcessed_WithEmptyTags()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessageProcessed("TestMessage", "TestHandler");
	}

	[Fact]
	public void RecordMessageProcessed_ThrowsOnNullTags()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordMessageProcessed("TestMessage", "TestHandler", null!));
	}

	#endregion

	#region RecordProcessingDuration Tests

	[Fact]
	public void RecordProcessingDuration_WithSuccessTrue()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordProcessingDuration(150.5, "TestMessage", true);
	}

	[Fact]
	public void RecordProcessingDuration_WithSuccessFalse()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordProcessingDuration(500.0, "TestMessage", false);
	}

	[Fact]
	public void RecordProcessingDuration_WithZeroDuration()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordProcessingDuration(0.0, "FastMessage", true);
	}

	[Fact]
	public void RecordProcessingDuration_WithLargeDuration()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordProcessingDuration(60000.0, "SlowMessage", true);
	}

	#endregion

	#region RecordMessagePublished Tests

	[Fact]
	public void RecordMessagePublished_WithValidParameters()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessagePublished("OrderCreated", "orders-queue");
	}

	[Fact]
	public void RecordMessagePublished_WithDifferentDestinations()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessagePublished("Event1", "queue-a");
		_metrics.RecordMessagePublished("Event2", "topic-b");
		_metrics.RecordMessagePublished("Event3", "direct-exchange");
	}

	#endregion

	#region RecordMessageFailed Tests

	[Fact]
	public void RecordMessageFailed_WithValidParameters()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessageFailed("TestMessage", "TimeoutException", 1);
	}

	[Fact]
	public void RecordMessageFailed_WithZeroRetryAttempt()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessageFailed("TestMessage", "ValidationException", 0);
	}

	[Fact]
	public void RecordMessageFailed_WithMultipleRetryAttempts()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessageFailed("TestMessage", "TransientException", 1);
		_metrics.RecordMessageFailed("TestMessage", "TransientException", 2);
		_metrics.RecordMessageFailed("TestMessage", "TransientException", 3);
	}

	[Theory]
	[InlineData("ArgumentException")]
	[InlineData("InvalidOperationException")]
	[InlineData("TimeoutException")]
	[InlineData("SerializationException")]
	public void RecordMessageFailed_WithVariousErrorTypes(string errorType)
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.RecordMessageFailed("TestMessage", errorType, 1);
	}

	#endregion

	#region UpdateActiveSessions Tests

	[Fact]
	public void UpdateActiveSessions_WithZero()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.UpdateActiveSessions(0);
	}

	[Fact]
	public void UpdateActiveSessions_WithPositiveValue()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.UpdateActiveSessions(100);
	}

	[Fact]
	public void UpdateActiveSessions_WithChangingValues()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		_metrics.UpdateActiveSessions(10);
		_metrics.UpdateActiveSessions(20);
		_metrics.UpdateActiveSessions(15);
		_metrics.UpdateActiveSessions(0);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange
		var metrics = new DispatchMetrics();

		// Assert
		metrics.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		metrics.Dispose();
	}

	[Fact]
	public void ImplementIDispatchMetrics()
	{
		// Arrange
		var metrics = new DispatchMetrics();

		// Assert
		metrics.ShouldBeAssignableTo<IDispatchMetrics>();

		metrics.Dispose();
	}

	#endregion

	#region Complete Workflow Tests

	[Fact]
	public void SupportCompleteMessageProcessingWorkflow()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act - Simulate a complete message processing workflow
		_metrics.UpdateActiveSessions(1);
		_metrics.RecordMessageProcessed("OrderCommand", "OrderHandler");
		_metrics.RecordProcessingDuration(150.0, "OrderCommand", true);
		_metrics.RecordMessagePublished("OrderCreatedEvent", "events-topic");
		_metrics.UpdateActiveSessions(0);

		// Assert - No exceptions means success
	}

	[Fact]
	public void SupportFailedMessageWorkflow()
	{
		// Arrange
		_metrics = new DispatchMetrics();

		// Act - Simulate a failed message processing workflow
		_metrics.UpdateActiveSessions(1);
		_metrics.RecordMessageProcessed("FailingCommand", "FailingHandler");
		_metrics.RecordProcessingDuration(50.0, "FailingCommand", false);
		_metrics.RecordMessageFailed("FailingCommand", "ValidationException", 0);
		_metrics.UpdateActiveSessions(0);

		// Assert - No exceptions means success
	}

	#endregion
}
