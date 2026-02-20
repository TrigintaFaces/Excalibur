// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.CosmosDb.Cdc;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbCdcRecoveryOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify recovery options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "CDC")]
public sealed class CosmosDbCdcRecoveryOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void RecoveryStrategy_DefaultsToThrow()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.Throw);
	}

	[Fact]
	public void OnPositionReset_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.OnPositionReset.ShouldBeNull();
	}

	[Fact]
	public void AutoRecreateProcessorOnInvalidToken_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.AutoRecreateProcessorOnInvalidToken.ShouldBeTrue();
	}

	[Fact]
	public void UseCurrentTimeOnResumeFailure_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.UseCurrentTimeOnResumeFailure.ShouldBeTrue();
	}

	[Fact]
	public void MaxRecoveryAttempts_DefaultsToThree()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(3);
	}

	[Fact]
	public void RecoveryAttemptDelay_DefaultsToOneSecond()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AlwaysInvokeCallbackOnReset_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.AlwaysInvokeCallbackOnReset.ShouldBeTrue();
	}

	[Fact]
	public void HandlePartitionSplitsGracefully_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbCdcRecoveryOptions();

		// Assert
		options.HandlePartitionSplitsGracefully.ShouldBeTrue();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		CdcPositionResetHandler handler = (_, _) => Task.CompletedTask;

		// Act
		var options = new CosmosDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest,
			OnPositionReset = handler,
			AutoRecreateProcessorOnInvalidToken = false,
			UseCurrentTimeOnResumeFailure = false,
			MaxRecoveryAttempts = 5,
			RecoveryAttemptDelay = TimeSpan.FromSeconds(2),
			AlwaysInvokeCallbackOnReset = false,
			HandlePartitionSplitsGracefully = false
		};

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToLatest);
		options.OnPositionReset.ShouldNotBeNull();
		options.AutoRecreateProcessorOnInvalidToken.ShouldBeFalse();
		options.UseCurrentTimeOnResumeFailure.ShouldBeFalse();
		options.MaxRecoveryAttempts.ShouldBe(5);
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.AlwaysInvokeCallbackOnReset.ShouldBeFalse();
		options.HandlePartitionSplitsGracefully.ShouldBeFalse();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithDefaultOptions()
	{
		// Arrange
		var options = new CosmosDbCdcRecoveryOptions();

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenCallbackStrategyWithoutHandler()
	{
		// Arrange
		var options = new CosmosDbCdcRecoveryOptions
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
		var options = new CosmosDbCdcRecoveryOptions
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
		var options = new CosmosDbCdcRecoveryOptions
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
		var options = new CosmosDbCdcRecoveryOptions
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
		var options = new CosmosDbCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.Zero
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
		var options = new CosmosDbCdcRecoveryOptions
		{
			RecoveryStrategy = strategy
		};

		// Assert
		options.RecoveryStrategy.ShouldBe(strategy);
	}

	#endregion
}
