// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="ConnectionPoolSizingOptions"/> -- the extracted sub-options class
/// for connection pool sizing and capacity (Sprint 630 A.2).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
public sealed class ConnectionPoolSizingOptionsShould
{
	#region Default Values

	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new ConnectionPoolSizingOptions();

		// Assert
		options.MinConnections.ShouldBe(0);
		options.MaxConnections.ShouldBe(100);
		options.MaxConnectionUseCount.ShouldBe(1000);
		options.PreWarmConnections.ShouldBeTrue();
		options.AcquisitionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxConnectionLifetime.ShouldBe(TimeSpan.FromMinutes(30));
	}

	#endregion

	#region Validation -- Happy Path

	[Fact]
	public void Validate_Succeeds_WithDefaults()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions();

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WhenMinEqualsMax()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions
		{
			MinConnections = 10,
			MaxConnections = 10
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	#endregion

	#region Validation -- Error Paths

	[Fact]
	public void Validate_Throws_WhenMinConnectionsNegative()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { MinConnections = -1 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenMaxConnectionsZero()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { MaxConnections = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenMaxConnectionsNegative()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { MaxConnections = -5 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenMinExceedsMax()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions
		{
			MinConnections = 50,
			MaxConnections = 10
		};

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenAcquisitionTimeoutZero()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { AcquisitionTimeout = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenAcquisitionTimeoutNegative()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { AcquisitionTimeout = TimeSpan.FromSeconds(-1) };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenIdleTimeoutZero()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { IdleTimeout = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenMaxConnectionLifetimeZero()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { MaxConnectionLifetime = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenMaxConnectionUseCountZero()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { MaxConnectionUseCount = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenMaxConnectionUseCountNegative()
	{
		// Arrange
		var options = new ConnectionPoolSizingOptions { MaxConnectionUseCount = -1 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	#endregion

	#region Clone

	[Fact]
	public void Clone_CreatesIdenticalCopy()
	{
		// Arrange
		var original = new ConnectionPoolSizingOptions
		{
			MinConnections = 5,
			MaxConnections = 50,
			MaxConnectionUseCount = 500,
			PreWarmConnections = false,
			AcquisitionTimeout = TimeSpan.FromSeconds(10),
			IdleTimeout = TimeSpan.FromMinutes(2),
			MaxConnectionLifetime = TimeSpan.FromMinutes(15)
		};

		// Act
		var clone = original.Clone();

		// Assert
		clone.ShouldNotBeSameAs(original);
		clone.MinConnections.ShouldBe(5);
		clone.MaxConnections.ShouldBe(50);
		clone.MaxConnectionUseCount.ShouldBe(500);
		clone.PreWarmConnections.ShouldBeFalse();
		clone.AcquisitionTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		clone.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		clone.MaxConnectionLifetime.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void Clone_IsIndependent()
	{
		// Arrange
		var original = new ConnectionPoolSizingOptions { MaxConnections = 50 };

		// Act
		var clone = original.Clone();
		clone.MaxConnections = 200;

		// Assert -- original is unchanged
		original.MaxConnections.ShouldBe(50);
	}

	#endregion
}
