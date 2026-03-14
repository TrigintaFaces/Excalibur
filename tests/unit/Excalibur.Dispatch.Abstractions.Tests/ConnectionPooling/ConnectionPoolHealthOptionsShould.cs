// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="ConnectionPoolHealthOptions"/> -- the extracted sub-options class
/// for connection pool health monitoring and failure handling (Sprint 630 A.2).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
public sealed class ConnectionPoolHealthOptionsShould
{
	#region Default Values

	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new ConnectionPoolHealthOptions();

		// Assert
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.CleanupInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.ValidateOnAcquisition.ShouldBeTrue();
		options.ValidateOnReturn.ShouldBeFalse();
		options.EnableHealthChecking.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.FailureHandling.ShouldBe(FailureHandlingStrategy.RetryThenFail);
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion

	#region Validation -- Happy Path

	[Fact]
	public void Validate_Succeeds_WithDefaults()
	{
		// Arrange
		var options = new ConnectionPoolHealthOptions();

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WhenMaxRetryAttemptsIsZero()
	{
		// Arrange -- 0 retries means fail immediately, which is valid
		var options = new ConnectionPoolHealthOptions { MaxRetryAttempts = 0 };

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WhenRetryDelayIsZero()
	{
		// Arrange -- 0 delay means no wait between retries
		var options = new ConnectionPoolHealthOptions { RetryDelay = TimeSpan.Zero };

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	#endregion

	#region Validation -- Error Paths

	[Fact]
	public void Validate_Throws_WhenHealthCheckIntervalZero()
	{
		// Arrange
		var options = new ConnectionPoolHealthOptions { HealthCheckInterval = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenHealthCheckIntervalNegative()
	{
		// Arrange
		var options = new ConnectionPoolHealthOptions { HealthCheckInterval = TimeSpan.FromSeconds(-1) };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenCleanupIntervalZero()
	{
		// Arrange
		var options = new ConnectionPoolHealthOptions { CleanupInterval = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenCleanupIntervalNegative()
	{
		// Arrange
		var options = new ConnectionPoolHealthOptions { CleanupInterval = TimeSpan.FromSeconds(-1) };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenMaxRetryAttemptsNegative()
	{
		// Arrange
		var options = new ConnectionPoolHealthOptions { MaxRetryAttempts = -1 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenRetryDelayNegative()
	{
		// Arrange
		var options = new ConnectionPoolHealthOptions { RetryDelay = TimeSpan.FromMilliseconds(-1) };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	#endregion

	#region Clone

	[Fact]
	public void Clone_CreatesIdenticalCopy()
	{
		// Arrange
		var original = new ConnectionPoolHealthOptions
		{
			HealthCheckInterval = TimeSpan.FromSeconds(30),
			CleanupInterval = TimeSpan.FromSeconds(15),
			ValidateOnAcquisition = false,
			ValidateOnReturn = true,
			EnableHealthChecking = false,
			EnableMetrics = false,
			FailureHandling = FailureHandlingStrategy.FailFast,
			MaxRetryAttempts = 5,
			RetryDelay = TimeSpan.FromMilliseconds(200)
		};

		// Act
		var clone = original.Clone();

		// Assert
		clone.ShouldNotBeSameAs(original);
		clone.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
		clone.CleanupInterval.ShouldBe(TimeSpan.FromSeconds(15));
		clone.ValidateOnAcquisition.ShouldBeFalse();
		clone.ValidateOnReturn.ShouldBeTrue();
		clone.EnableHealthChecking.ShouldBeFalse();
		clone.EnableMetrics.ShouldBeFalse();
		clone.FailureHandling.ShouldBe(FailureHandlingStrategy.FailFast);
		clone.MaxRetryAttempts.ShouldBe(5);
		clone.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
	}

	[Fact]
	public void Clone_IsIndependent()
	{
		// Arrange
		var original = new ConnectionPoolHealthOptions { MaxRetryAttempts = 5 };

		// Act
		var clone = original.Clone();
		clone.MaxRetryAttempts = 10;

		// Assert -- original is unchanged
		original.MaxRetryAttempts.ShouldBe(5);
	}

	#endregion
}
