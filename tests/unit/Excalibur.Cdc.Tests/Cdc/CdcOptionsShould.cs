// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcOptions"/> and <see cref="CdcTableTrackingOptions"/>.
/// Tests the CDC options configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcOptionsShould : UnitTestBase
{
	#region CdcOptions Default Values Tests

	[Fact]
	public void HasCorrectDefaults()
	{
		// Arrange & Act
		var options = new CdcOptions();

		// Assert
		options.TrackedTables.ShouldBeEmpty();
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		options.OnPositionReset.ShouldBeNull();
		options.MaxRecoveryAttempts.ShouldBe(3);
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.EnableStructuredLogging.ShouldBeTrue();
		options.EnableBackgroundProcessing.ShouldBeFalse();
	}

	#endregion

	#region CdcOptions Property Tests

	[Fact]
	public void RecoveryStrategy_CanBeSet()
	{
		// Arrange
		var options = new CdcOptions();

		// Act
		options.RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest;

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToLatest);
	}

	[Fact]
	public void MaxRecoveryAttempts_CanBeSet()
	{
		// Arrange
		var options = new CdcOptions();

		// Act
		options.MaxRecoveryAttempts = 10;

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(10);
	}

	[Fact]
	public void RecoveryAttemptDelay_CanBeSet()
	{
		// Arrange
		var options = new CdcOptions();

		// Act
		options.RecoveryAttemptDelay = TimeSpan.FromSeconds(30);

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void EnableStructuredLogging_CanBeSet()
	{
		// Arrange
		var options = new CdcOptions();

		// Act
		options.EnableStructuredLogging = false;

		// Assert
		options.EnableStructuredLogging.ShouldBeFalse();
	}

	[Fact]
	public void EnableBackgroundProcessing_CanBeSet()
	{
		// Arrange
		var options = new CdcOptions();

		// Act
		options.EnableBackgroundProcessing = true;

		// Assert
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void OnPositionReset_CanBeSet()
	{
		// Arrange
		var options = new CdcOptions();
		CdcPositionResetHandler handler = (_, _) => Task.CompletedTask;

		// Act
		options.OnPositionReset = handler;

		// Assert
		options.OnPositionReset.ShouldBe(handler);
	}

	#endregion

	#region CdcOptions Validate Tests

	[Fact]
	public void Validate_Succeeds_WithDefaultOptions()
	{
		// Arrange
		var options = new CdcOptions();

		// Act & Assert - should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithInvokeCallbackStrategyAndHandler()
	{
		// Arrange
		var options = new CdcOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (_, _) => Task.CompletedTask
		};

		// Act & Assert - should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenInvokeCallbackStrategyWithoutHandler()
	{
		// Arrange
		var options = new CdcOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = null
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("OnPositionReset");
		ex.Message.ShouldContain("InvokeCallback");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenMaxRecoveryAttemptsNegative()
	{
		// Arrange
		var options = new CdcOptions { MaxRecoveryAttempts = -1 };

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("MaxRecoveryAttempts");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenRecoveryAttemptDelayNegative()
	{
		// Arrange
		var options = new CdcOptions { RecoveryAttemptDelay = TimeSpan.FromSeconds(-1) };

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("RecoveryAttemptDelay");
	}

	[Fact]
	public void Validate_Succeeds_WithZeroMaxRecoveryAttempts()
	{
		// Arrange
		var options = new CdcOptions { MaxRecoveryAttempts = 0 };

		// Act & Assert - should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithZeroRecoveryAttemptDelay()
	{
		// Arrange
		var options = new CdcOptions { RecoveryAttemptDelay = TimeSpan.Zero };

		// Act & Assert - should not throw
		options.Validate();
	}

	[Theory]
	[InlineData(StalePositionRecoveryStrategy.Throw)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToEarliest)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToLatest)]
	public void Validate_Succeeds_WithOtherStrategiesWithoutHandler(StalePositionRecoveryStrategy strategy)
	{
		// Arrange
		var options = new CdcOptions
		{
			RecoveryStrategy = strategy,
			OnPositionReset = null
		};

		// Act & Assert - should not throw
		options.Validate();
	}

	#endregion

	#region CdcTableTrackingOptions Tests

	[Fact]
	public void CdcTableTrackingOptions_HasCorrectDefaults()
	{
		// Arrange & Act
		var options = new CdcTableTrackingOptions();

		// Assert
		options.TableName.ShouldBe(string.Empty);
		options.CaptureInstance.ShouldBeNull();
		options.EventMappings.ShouldBeEmpty();
		options.Filter.ShouldBeNull();
	}

	[Fact]
	public void CdcTableTrackingOptions_TableName_CanBeSet()
	{
		// Arrange
		var options = new CdcTableTrackingOptions();

		// Act
		options.TableName = "dbo.Orders";

		// Assert
		options.TableName.ShouldBe("dbo.Orders");
	}

	[Fact]
	public void CdcTableTrackingOptions_CaptureInstance_CanBeSet()
	{
		// Arrange
		var options = new CdcTableTrackingOptions();

		// Act
		options.CaptureInstance = "dbo_Orders_CT";

		// Assert
		options.CaptureInstance.ShouldBe("dbo_Orders_CT");
	}

	[Fact]
	public void CdcTableTrackingOptions_Filter_CanBeSet()
	{
		// Arrange
		var options = new CdcTableTrackingOptions();
		Func<CdcDataChange, bool> filter = change => change.ColumnName != "UpdatedAt";

		// Act
		options.Filter = filter;

		// Assert
		options.Filter.ShouldBe(filter);
	}

	[Fact]
	public void CdcTableTrackingOptions_EventMappings_CanBeModified()
	{
		// Arrange
		var options = new CdcTableTrackingOptions();

		// Act
		options.EventMappings[CdcChangeType.Insert] = typeof(OrderCreatedEvent);
		options.EventMappings[CdcChangeType.Update] = typeof(OrderUpdatedEvent);
		options.EventMappings[CdcChangeType.Delete] = typeof(OrderDeletedEvent);

		// Assert
		options.EventMappings.Count.ShouldBe(3);
		options.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(OrderCreatedEvent));
		options.EventMappings[CdcChangeType.Update].ShouldBe(typeof(OrderUpdatedEvent));
		options.EventMappings[CdcChangeType.Delete].ShouldBe(typeof(OrderDeletedEvent));
	}

	#endregion

	// Test event types
	private sealed class OrderCreatedEvent { }
	private sealed class OrderUpdatedEvent { }
	private sealed class OrderDeletedEvent { }
}
