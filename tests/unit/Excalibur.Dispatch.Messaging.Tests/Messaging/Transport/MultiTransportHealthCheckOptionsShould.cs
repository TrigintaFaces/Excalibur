// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="MultiTransportHealthCheckOptions"/>.
/// </summary>
/// <remarks>
/// Tests the multi-transport health check configuration options.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class MultiTransportHealthCheckOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_RequireAtLeastOneTransport_IsFalse()
	{
		// Arrange & Act
		var options = new MultiTransportHealthCheckOptions();

		// Assert
		options.RequireAtLeastOneTransport.ShouldBeFalse();
	}

	[Fact]
	public void Default_RequireDefaultTransportHealthy_IsTrue()
	{
		// Arrange & Act
		var options = new MultiTransportHealthCheckOptions();

		// Assert
		options.RequireDefaultTransportHealthy.ShouldBeTrue();
	}

	[Fact]
	public void Default_TransportCheckTimeout_Is5Seconds()
	{
		// Arrange & Act
		var options = new MultiTransportHealthCheckOptions();

		// Assert
		options.TransportCheckTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Default_ParallelChecks_IsTrue()
	{
		// Arrange & Act
		var options = new MultiTransportHealthCheckOptions();

		// Assert
		options.ParallelChecks.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void RequireAtLeastOneTransport_CanBeSetToTrue()
	{
		// Arrange
		var options = new MultiTransportHealthCheckOptions();

		// Act
		options.RequireAtLeastOneTransport = true;

		// Assert
		options.RequireAtLeastOneTransport.ShouldBeTrue();
	}

	[Fact]
	public void RequireDefaultTransportHealthy_CanBeSetToFalse()
	{
		// Arrange
		var options = new MultiTransportHealthCheckOptions();

		// Act
		options.RequireDefaultTransportHealthy = false;

		// Assert
		options.RequireDefaultTransportHealthy.ShouldBeFalse();
	}

	[Fact]
	public void TransportCheckTimeout_CanBeSet()
	{
		// Arrange
		var options = new MultiTransportHealthCheckOptions();
		var timeout = TimeSpan.FromSeconds(10);

		// Act
		options.TransportCheckTimeout = timeout;

		// Assert
		options.TransportCheckTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void TransportCheckTimeout_CanBeSetToZero()
	{
		// Arrange
		var options = new MultiTransportHealthCheckOptions();

		// Act
		options.TransportCheckTimeout = TimeSpan.Zero;

		// Assert
		options.TransportCheckTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void TransportCheckTimeout_CanBeSetToLargeValue()
	{
		// Arrange
		var options = new MultiTransportHealthCheckOptions();
		var timeout = TimeSpan.FromMinutes(5);

		// Act
		options.TransportCheckTimeout = timeout;

		// Assert
		options.TransportCheckTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void ParallelChecks_CanBeSetToFalse()
	{
		// Arrange
		var options = new MultiTransportHealthCheckOptions();

		// Act
		options.ParallelChecks = false;

		// Assert
		options.ParallelChecks.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange & Act
		var options = new MultiTransportHealthCheckOptions
		{
			RequireAtLeastOneTransport = true,
			RequireDefaultTransportHealthy = false,
			TransportCheckTimeout = TimeSpan.FromSeconds(15),
			ParallelChecks = false,
		};

		// Assert
		options.RequireAtLeastOneTransport.ShouldBeTrue();
		options.RequireDefaultTransportHealthy.ShouldBeFalse();
		options.TransportCheckTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.ParallelChecks.ShouldBeFalse();
	}

	#endregion

	#region Configuration Scenarios Tests

	[Fact]
	public void StrictConfiguration_RequiresAllChecks()
	{
		// Arrange - Strict configuration for production
		var options = new MultiTransportHealthCheckOptions
		{
			RequireAtLeastOneTransport = true,
			RequireDefaultTransportHealthy = true,
			TransportCheckTimeout = TimeSpan.FromSeconds(3),
			ParallelChecks = true,
		};

		// Assert
		options.RequireAtLeastOneTransport.ShouldBeTrue();
		options.RequireDefaultTransportHealthy.ShouldBeTrue();
		options.TransportCheckTimeout.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void LenientConfiguration_RelaxesAllChecks()
	{
		// Arrange - Lenient configuration for development
		var options = new MultiTransportHealthCheckOptions
		{
			RequireAtLeastOneTransport = false,
			RequireDefaultTransportHealthy = false,
			TransportCheckTimeout = TimeSpan.FromSeconds(30),
			ParallelChecks = false,
		};

		// Assert
		options.RequireAtLeastOneTransport.ShouldBeFalse();
		options.RequireDefaultTransportHealthy.ShouldBeFalse();
		options.TransportCheckTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.ParallelChecks.ShouldBeFalse();
	}

	#endregion
}
