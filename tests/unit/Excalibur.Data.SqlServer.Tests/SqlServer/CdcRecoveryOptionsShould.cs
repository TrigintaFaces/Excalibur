// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcRecoveryOptions"/>.
/// Tests CDC recovery configuration and validation.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// bd-1710x: CDC Recovery Infrastructure Tests.
/// Updated: Uses consolidated core StalePositionRecoveryStrategy.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "CdcRecoveryOptions")]
public sealed class CdcRecoveryOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Act
		var options = new CdcRecoveryOptions();

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		options.OnPositionReset.ShouldBeNull();
		options.MaxRecoveryAttempts.ShouldBe(3);
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.EnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingRecoveryStrategy()
	{
		// Arrange
		var options = new CdcRecoveryOptions();

		// Act
		options.RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest;

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToLatest);
	}

	[Fact]
	public async Task AllowSettingOnPositionResetCallback()
	{
		// Arrange
		var options = new CdcRecoveryOptions();
		var callbackInvoked = false;

		// Act
		options.OnPositionReset = (args, ct) =>
		{
			callbackInvoked = true;
			return Task.CompletedTask;
		};

		// Invoke to verify
		await options.OnPositionReset.Invoke(CreateTestEventArgs(), CancellationToken.None);

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMaxRecoveryAttempts()
	{
		// Arrange
		var options = new CdcRecoveryOptions();

		// Act
		options.MaxRecoveryAttempts = 5;

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingRecoveryAttemptDelay()
	{
		// Arrange
		var options = new CdcRecoveryOptions();

		// Act
		options.RecoveryAttemptDelay = TimeSpan.FromSeconds(5);

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void AllowDisablingStructuredLogging()
	{
		// Arrange
		var options = new CdcRecoveryOptions();

		// Act
		options.EnableStructuredLogging = false;

		// Assert
		options.EnableStructuredLogging.ShouldBeFalse();
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaultConfiguration()
	{
		// Arrange
		var options = new CdcRecoveryOptions();

		// Act & Assert (should not throw)
		options.Validate();
	}

	[Fact]
	public void ValidateSuccessfullyWithThrowStrategy()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.Throw
		};

		// Act & Assert (should not throw)
		options.Validate();
	}

	[Fact]
	public void ValidateSuccessfullyWithFallbackToLatestStrategy()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest
		};

		// Act & Assert (should not throw)
		options.Validate();
	}

	[Fact]
	public void ValidateSuccessfullyWithInvokeCallbackStrategyAndCallback()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (args, ct) => Task.CompletedTask
		};

		// Act & Assert (should not throw)
		options.Validate();
	}

	[Fact]
	public void ThrowWhenInvokeCallbackStrategyWithoutCallback()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = null
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("OnPositionReset");
		exception.Message.ShouldContain("InvokeCallback");
	}

	[Fact]
	public void ThrowWhenMaxRecoveryAttemptsIsNegative()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			MaxRecoveryAttempts = -1
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("MaxRecoveryAttempts");
	}

	[Fact]
	public void AllowZeroMaxRecoveryAttempts()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			MaxRecoveryAttempts = 0
		};

		// Act & Assert (should not throw)
		options.Validate();
	}

	[Fact]
	public void ThrowWhenRecoveryAttemptDelayIsNegative()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("RecoveryAttemptDelay");
	}

	[Fact]
	public void AllowZeroRecoveryAttemptDelay()
	{
		// Arrange
		var options = new CdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.Zero
		};

		// Act & Assert (should not throw)
		options.Validate();
	}

	#region FromCdcOptions Factory Tests

	[Fact]
	public void FromCdcOptions_ThrowsArgumentNullException_WhenCdcOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			CdcRecoveryOptions.FromCdcOptions(null!));
	}

	[Fact]
	public void FromCdcOptions_CopiesDefaultValues()
	{
		// Arrange
		var cdcOptions = new CdcOptions();

		// Act
		var result = CdcRecoveryOptions.FromCdcOptions(cdcOptions);

		// Assert — all recovery-related properties should match CdcOptions defaults
		result.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		result.OnPositionReset.ShouldBeNull();
		result.MaxRecoveryAttempts.ShouldBe(3);
		result.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
		result.EnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void FromCdcOptions_CopiesCustomValues()
	{
		// Arrange
		CdcPositionResetHandler callback = (args, ct) => Task.CompletedTask;
		var cdcOptions = new CdcOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = callback,
			MaxRecoveryAttempts = 7,
			RecoveryAttemptDelay = TimeSpan.FromSeconds(5),
			EnableStructuredLogging = false,
		};

		// Act
		var result = CdcRecoveryOptions.FromCdcOptions(cdcOptions);

		// Assert — all custom values propagated
		result.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		result.OnPositionReset.ShouldBeSameAs(callback);
		result.MaxRecoveryAttempts.ShouldBe(7);
		result.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(5));
		result.EnableStructuredLogging.ShouldBeFalse();
	}

	[Fact]
	public void FromCdcOptions_ResultPassesValidation()
	{
		// Arrange
		var cdcOptions = new CdcOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest,
			MaxRecoveryAttempts = 1,
		};

		// Act
		var result = CdcRecoveryOptions.FromCdcOptions(cdcOptions);

		// Assert — should not throw
		result.Validate();
	}

	#endregion

	private static CdcPositionResetEventArgs CreateTestEventArgs()
	{
		return new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange
		};
	}
}
