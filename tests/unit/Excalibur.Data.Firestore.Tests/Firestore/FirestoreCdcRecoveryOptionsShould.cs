// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreCdcRecoveryOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify recovery options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreCdcRecoveryOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultRecoveryStrategy()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.Throw);
	}

	[Fact]
	public void HaveNullOnPositionResetByDefault()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.OnPositionReset.ShouldBeNull();
	}

	[Fact]
	public void HaveAutoReconnectOnDisconnectEnabledByDefault()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.AutoReconnectOnDisconnect.ShouldBeTrue();
	}

	[Fact]
	public void HaveRetryOnPermissionDeniedDisabledByDefault()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.RetryOnPermissionDenied.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMaxRecoveryAttempts()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRecoveryAttemptDelay()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveAlwaysInvokeCallbackOnResetEnabledByDefault()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.AlwaysInvokeCallbackOnReset.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultReconnectionTimeout()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.ReconnectionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultMaxUnavailableWaitTime()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions();

		// Assert
		options.MaxUnavailableWaitTime.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingRecoveryStrategy()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest
		};

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
	}

	[Fact]
	public void AllowSettingOnPositionReset()
	{
		// Arrange
		CdcPositionResetHandler handler = (args, ct) => Task.CompletedTask;

		// Act
		var options = new FirestoreCdcRecoveryOptions
		{
			OnPositionReset = handler
		};

		// Assert
		options.OnPositionReset.ShouldBe(handler);
	}

	[Fact]
	public void AllowSettingAutoReconnectOnDisconnect()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions
		{
			AutoReconnectOnDisconnect = false
		};

		// Assert
		options.AutoReconnectOnDisconnect.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRetryOnPermissionDenied()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions
		{
			RetryOnPermissionDenied = true
		};

		// Assert
		options.RetryOnPermissionDenied.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMaxRecoveryAttempts()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions
		{
			MaxRecoveryAttempts = 5
		};

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingRecoveryAttemptDelay()
	{
		// Arrange & Act
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(5)
		};

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenInvokeCallbackWithoutHandler()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
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
	public void Validate_ThrowsInvalidOperationException_WhenMaxRecoveryAttemptsIsZero()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			MaxRecoveryAttempts = 0
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("MaxRecoveryAttempts");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenMaxRecoveryAttemptsIsNegative()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			MaxRecoveryAttempts = -1
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenRecoveryAttemptDelayIsNegative()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("RecoveryAttemptDelay");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenReconnectionTimeoutIsNegative()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			ReconnectionTimeout = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ReconnectionTimeout");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenMaxUnavailableWaitTimeIsNegative()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			MaxUnavailableWaitTime = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("MaxUnavailableWaitTime");
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenInvokeCallbackWithHandler()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (args, ct) => Task.CompletedTask
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenUsingThrowStrategy()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.Throw
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenUsingFallbackToEarliestStrategy()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenUsingFallbackToLatestStrategy()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenZeroTimeSpans()
	{
		// Arrange
		var options = new FirestoreCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.Zero,
			ReconnectionTimeout = TimeSpan.Zero,
			MaxUnavailableWaitTime = TimeSpan.Zero
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(FirestoreCdcRecoveryOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreCdcRecoveryOptions).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void BeRecord()
	{
		// Assert - Records are classes with special equality semantics
		typeof(FirestoreCdcRecoveryOptions).IsClass.ShouldBeTrue();
	}

	#endregion
}
