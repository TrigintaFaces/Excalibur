// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="UnifiedConnectionPoolOptions{TConnection}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
[Trait("Priority", "0")]
public sealed class UnifiedConnectionPoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MinConnections_IsOne()
	{
		// Arrange & Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Assert
		options.MinConnections.ShouldBe(1);
	}

	[Fact]
	public void Default_MaxConnections_IsTen()
	{
		// Arrange & Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Assert
		options.MaxConnections.ShouldBe(10);
	}

	[Fact]
	public void Default_ConnectionTimeout_Is30Seconds()
	{
		// Arrange & Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Assert
		options.ConnectionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_IdleTimeout_Is5Minutes()
	{
		// Arrange & Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Assert
		options.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_ValidateOnCheckout_IsTrue()
	{
		// Arrange & Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Assert
		options.ValidateOnCheckout.ShouldBeTrue();
	}

	[Fact]
	public void Default_HealthCheckInterval_Is1Minute()
	{
		// Arrange & Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Assert
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Default_ConnectionConfigurations_IsEmptyDictionary()
	{
		// Arrange & Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Assert
		_ = options.ConnectionConfigurations.ShouldNotBeNull();
		options.ConnectionConfigurations.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MinConnections_CanBeSet()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.MinConnections = 5;

		// Assert
		options.MinConnections.ShouldBe(5);
	}

	[Fact]
	public void MaxConnections_CanBeSet()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.MaxConnections = 50;

		// Assert
		options.MaxConnections.ShouldBe(50);
	}

	[Fact]
	public void ConnectionTimeout_CanBeSet()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.ConnectionTimeout = TimeSpan.FromMinutes(1);

		// Assert
		options.ConnectionTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void IdleTimeout_CanBeSet()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.IdleTimeout = TimeSpan.FromMinutes(10);

		// Assert
		options.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void ValidateOnCheckout_CanBeSetToFalse()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.ValidateOnCheckout = false;

		// Assert
		options.ValidateOnCheckout.ShouldBeFalse();
	}

	[Fact]
	public void HealthCheckInterval_CanBeSet()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.HealthCheckInterval = TimeSpan.FromSeconds(30);

		// Assert
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region ConnectionConfigurations Tests

	[Fact]
	public void ConnectionConfigurations_CanAddConfiguration()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();
		var config = new ConnectionConfiguration<TestConnection>();

		// Act
		options.ConnectionConfigurations["primary"] = config;

		// Assert
		options.ConnectionConfigurations.ShouldContainKey("primary");
		options.ConnectionConfigurations["primary"].ShouldBe(config);
	}

	[Fact]
	public void ConnectionConfigurations_CanAddMultipleConfigurations()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();
		var primary = new ConnectionConfiguration<TestConnection>();
		var secondary = new ConnectionConfiguration<TestConnection>();

		// Act
		options.ConnectionConfigurations["primary"] = primary;
		options.ConnectionConfigurations["secondary"] = secondary;

		// Assert
		options.ConnectionConfigurations.Count.ShouldBe(2);
		options.ConnectionConfigurations["primary"].ShouldBe(primary);
		options.ConnectionConfigurations["secondary"].ShouldBe(secondary);
	}

	[Fact]
	public void ConnectionConfigurations_UsesCaseSensitiveKeys()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();
		var config1 = new ConnectionConfiguration<TestConnection>();
		var config2 = new ConnectionConfiguration<TestConnection>();

		// Act
		options.ConnectionConfigurations["Primary"] = config1;
		options.ConnectionConfigurations["primary"] = config2;

		// Assert
		options.ConnectionConfigurations.Count.ShouldBe(2);
	}

	[Fact]
	public void ConnectionConfigurations_CanRemoveConfiguration()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();
		options.ConnectionConfigurations["test"] = new ConnectionConfiguration<TestConnection>();

		// Act
		var removed = options.ConnectionConfigurations.Remove("test");

		// Assert
		removed.ShouldBeTrue();
		options.ConnectionConfigurations.ShouldBeEmpty();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new UnifiedConnectionPoolOptions<TestConnection>
		{
			MinConnections = 2,
			MaxConnections = 20,
			ConnectionTimeout = TimeSpan.FromSeconds(15),
			IdleTimeout = TimeSpan.FromMinutes(2),
			ValidateOnCheckout = false,
			HealthCheckInterval = TimeSpan.FromSeconds(45),
		};

		// Assert
		options.MinConnections.ShouldBe(2);
		options.MaxConnections.ShouldBe(20);
		options.ConnectionTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.ValidateOnCheckout.ShouldBeFalse();
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(45));
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void MinConnections_CanBeZero()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.MinConnections = 0;

		// Assert
		options.MinConnections.ShouldBe(0);
	}

	[Fact]
	public void ConnectionTimeout_CanBeZero()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.ConnectionTimeout = TimeSpan.Zero;

		// Assert
		options.ConnectionTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HealthCheckInterval_CanBeMaxValue()
	{
		// Arrange
		var options = new UnifiedConnectionPoolOptions<TestConnection>();

		// Act
		options.HealthCheckInterval = TimeSpan.MaxValue;

		// Assert
		options.HealthCheckInterval.ShouldBe(TimeSpan.MaxValue);
	}

	#endregion

	#region Test Helpers

	private sealed class TestConnection;

	#endregion
}
