// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcRecoveryOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify recovery options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbCdcRecoveryOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void RecoveryStrategy_DefaultsToThrow()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.Throw);
	}

	[Fact]
	public void OnPositionReset_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.OnPositionReset.ShouldBeNull();
	}

	[Fact]
	public void AutoRefreshExpiredIterators_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.AutoRefreshExpiredIterators.ShouldBeTrue();
	}

	[Fact]
	public void HandleShardSplitsGracefully_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.HandleShardSplitsGracefully.ShouldBeTrue();
	}

	[Fact]
	public void MaxRecoveryAttempts_DefaultsToThree()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(3);
	}

	[Fact]
	public void RecoveryAttemptDelay_DefaultsToOneSecond()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AlwaysInvokeCallbackOnReset_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.AlwaysInvokeCallbackOnReset.ShouldBeTrue();
	}

	[Fact]
	public void IteratorRefreshInterval_DefaultsToTenMinutes()
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions();

		// Assert
		options.IteratorRefreshInterval.ShouldBe(TimeSpan.FromMinutes(10));
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		CdcPositionResetHandler handler = (_, _) => Task.CompletedTask;

		// Act
		var options = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest,
			OnPositionReset = handler,
			AutoRefreshExpiredIterators = false,
			HandleShardSplitsGracefully = false,
			MaxRecoveryAttempts = 5,
			RecoveryAttemptDelay = TimeSpan.FromSeconds(2),
			AlwaysInvokeCallbackOnReset = false,
			IteratorRefreshInterval = TimeSpan.FromMinutes(5)
		};

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToLatest);
		options.OnPositionReset.ShouldNotBeNull();
		options.AutoRefreshExpiredIterators.ShouldBeFalse();
		options.HandleShardSplitsGracefully.ShouldBeFalse();
		options.MaxRecoveryAttempts.ShouldBe(5);
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.AlwaysInvokeCallbackOnReset.ShouldBeFalse();
		options.IteratorRefreshInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithDefaultOptions()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions();

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenCallbackStrategyWithoutHandler()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = null
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WhenCallbackStrategyWithHandler()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (_, _) => Task.CompletedTask
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenMaxRecoveryAttemptsLessThanOne()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			MaxRecoveryAttempts = 0
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenRecoveryAttemptDelayIsNegative()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithZeroDelay()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.Zero
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenIteratorRefreshIntervalIsNegative()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			IteratorRefreshInterval = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenIteratorRefreshIntervalExceedsFourteenMinutes()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			IteratorRefreshInterval = TimeSpan.FromMinutes(15)
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithFourteenMinuteRefreshInterval()
	{
		// Arrange
		var options = new DynamoDbCdcRecoveryOptions
		{
			IteratorRefreshInterval = TimeSpan.FromMinutes(14)
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	#endregion

	#region Recovery Strategy Tests

	[Theory]
	[InlineData(StalePositionRecoveryStrategy.Throw)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToEarliest)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToLatest)]
	[InlineData(StalePositionRecoveryStrategy.InvokeCallback)]
	public void RecoveryStrategy_AcceptsAllValues(StalePositionRecoveryStrategy strategy)
	{
		// Arrange & Act
		var options = new DynamoDbCdcRecoveryOptions
		{
			RecoveryStrategy = strategy
		};

		// Assert
		options.RecoveryStrategy.ShouldBe(strategy);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsRecord()
	{
		// Assert
		typeof(DynamoDbCdcRecoveryOptions).IsValueType.ShouldBeFalse();
		typeof(DynamoDbCdcRecoveryOptions).BaseType.ShouldNotBeNull();
	}

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(DynamoDbCdcRecoveryOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbCdcRecoveryOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
