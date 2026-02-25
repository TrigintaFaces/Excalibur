// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="ConnectionPoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
[Trait("Priority", "0")]
public sealed class ConnectionPoolOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_PoolNameIsDefaultPoolName()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.PoolName.ShouldBe(ConnectionPoolOptions.DefaultPoolName);
	}

	[Fact]
	public void DefaultPoolNameConstant_IsDefaultPool()
	{
		// Assert
		ConnectionPoolOptions.DefaultPoolName.ShouldBe("DefaultPool");
	}

	[Fact]
	public void Default_ConnectionStringIsEmpty()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_MinConnectionsIsZero()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.MinConnections.ShouldBe(0);
	}

	[Fact]
	public void Default_MaxConnectionsIs100()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.MaxConnections.ShouldBe(100);
	}

	[Fact]
	public void Default_AcquisitionTimeoutIs30Seconds()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.AcquisitionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_IdleTimeoutIs5Minutes()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_MaxConnectionLifetimeIs30Minutes()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.MaxConnectionLifetime.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void Default_HealthCheckIntervalIs1Minute()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Default_CleanupIntervalIs30Seconds()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_PreWarmConnectionsIsTrue()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.PreWarmConnections.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateOnAcquisitionIsTrue()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.ValidateOnAcquisition.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateOnReturnIsFalse()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.ValidateOnReturn.ShouldBeFalse();
	}

	[Fact]
	public void Default_EnableMetricsIsTrue()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableHealthCheckingIsTrue()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.EnableHealthChecking.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxConnectionUseCountIs1000()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.MaxConnectionUseCount.ShouldBe(1000);
	}

	[Fact]
	public void Default_FailureHandlingIsRetryThenFail()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.FailureHandling.ShouldBe(FailureHandlingStrategy.RetryThenFail);
	}

	[Fact]
	public void Default_MaxRetryAttemptsIs3()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Default_RetryDelayIs100Milliseconds()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void Default_PropertiesIsEmpty()
	{
		// Arrange & Act
		var options = new ConnectionPoolOptions();

		// Assert
		options.Properties.ShouldBeEmpty();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_WithValidOptions_DoesNotThrow()
	{
		// Arrange
		var options = new ConnectionPoolOptions();

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNullPoolName_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { PoolName = null! };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithEmptyPoolName_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { PoolName = string.Empty };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithWhitespacePoolName_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { PoolName = "   " };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNegativeMinConnections_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { MinConnections = -1 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroMaxConnections_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { MaxConnections = 0 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNegativeMaxConnections_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { MaxConnections = -1 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithMinConnectionsGreaterThanMax_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { MinConnections = 100, MaxConnections = 50 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroAcquisitionTimeout_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { AcquisitionTimeout = TimeSpan.Zero };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNegativeAcquisitionTimeout_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { AcquisitionTimeout = TimeSpan.FromSeconds(-1) };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroIdleTimeout_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { IdleTimeout = TimeSpan.Zero };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroMaxConnectionLifetime_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { MaxConnectionLifetime = TimeSpan.Zero };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroHealthCheckInterval_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { HealthCheckInterval = TimeSpan.Zero };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroCleanupInterval_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { CleanupInterval = TimeSpan.Zero };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNegativeMaxRetryAttempts_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { MaxRetryAttempts = -1 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNegativeRetryDelay_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { RetryDelay = TimeSpan.FromMilliseconds(-1) };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroMaxConnectionUseCount_ThrowsArgumentException()
	{
		// Arrange
		var options = new ConnectionPoolOptions { MaxConnectionUseCount = 0 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	#endregion

	#region Clone Tests

	[Fact]
	public void Clone_CreatesIdenticalCopy()
	{
		// Arrange
		var original = new ConnectionPoolOptions
		{
			PoolName = "TestPool",
			ConnectionString = "Server=localhost",
			MinConnections = 5,
			MaxConnections = 50,
			AcquisitionTimeout = TimeSpan.FromSeconds(10),
			IdleTimeout = TimeSpan.FromMinutes(2),
			MaxConnectionLifetime = TimeSpan.FromMinutes(15),
			HealthCheckInterval = TimeSpan.FromSeconds(30),
			CleanupInterval = TimeSpan.FromSeconds(15),
			PreWarmConnections = false,
			ValidateOnAcquisition = false,
			ValidateOnReturn = true,
			EnableMetrics = false,
			EnableHealthChecking = false,
			MaxConnectionUseCount = 500,
			FailureHandling = FailureHandlingStrategy.FailFast,
			MaxRetryAttempts = 5,
			RetryDelay = TimeSpan.FromMilliseconds(200),
		};
		original.Properties["key1"] = "value1";

		// Act
		var clone = original.Clone();

		// Assert
		clone.PoolName.ShouldBe(original.PoolName);
		clone.ConnectionString.ShouldBe(original.ConnectionString);
		clone.MinConnections.ShouldBe(original.MinConnections);
		clone.MaxConnections.ShouldBe(original.MaxConnections);
		clone.AcquisitionTimeout.ShouldBe(original.AcquisitionTimeout);
		clone.IdleTimeout.ShouldBe(original.IdleTimeout);
		clone.MaxConnectionLifetime.ShouldBe(original.MaxConnectionLifetime);
		clone.HealthCheckInterval.ShouldBe(original.HealthCheckInterval);
		clone.CleanupInterval.ShouldBe(original.CleanupInterval);
		clone.PreWarmConnections.ShouldBe(original.PreWarmConnections);
		clone.ValidateOnAcquisition.ShouldBe(original.ValidateOnAcquisition);
		clone.ValidateOnReturn.ShouldBe(original.ValidateOnReturn);
		clone.EnableMetrics.ShouldBe(original.EnableMetrics);
		clone.EnableHealthChecking.ShouldBe(original.EnableHealthChecking);
		clone.MaxConnectionUseCount.ShouldBe(original.MaxConnectionUseCount);
		clone.FailureHandling.ShouldBe(original.FailureHandling);
		clone.MaxRetryAttempts.ShouldBe(original.MaxRetryAttempts);
		clone.RetryDelay.ShouldBe(original.RetryDelay);
		clone.Properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public void Clone_CreatesNewInstance()
	{
		// Arrange
		var original = new ConnectionPoolOptions();

		// Act
		var clone = original.Clone();

		// Assert
		clone.ShouldNotBeSameAs(original);
	}

	[Fact]
	public void Clone_CopiesProperties()
	{
		// Arrange
		var original = new ConnectionPoolOptions();
		original.Properties["custom"] = "value";

		// Act
		var clone = original.Clone();

		// Assert
		clone.Properties.ShouldContainKeyAndValue("custom", "value");
		clone.Properties.ShouldNotBeSameAs(original.Properties);
	}

	#endregion
}
