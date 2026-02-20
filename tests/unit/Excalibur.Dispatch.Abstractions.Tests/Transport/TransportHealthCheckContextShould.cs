// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="TransportHealthCheckContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportHealthCheckContextShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsRequestedCategories()
	{
		// Arrange & Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		// Assert
		context.RequestedCategories.ShouldBe(TransportHealthCheckCategory.Connectivity);
	}

	[Fact]
	public void Constructor_WithAllCategories_SetsAllCategories()
	{
		// Arrange & Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.All);

		// Assert
		context.RequestedCategories.ShouldBe(TransportHealthCheckCategory.All);
	}

	[Fact]
	public void Constructor_WithCombinedCategories_SetsCombinedCategories()
	{
		// Arrange
		var categories = TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Performance;

		// Act
		var context = new TransportHealthCheckContext(categories);

		// Assert
		context.RequestedCategories.HasFlag(TransportHealthCheckCategory.Connectivity).ShouldBeTrue();
		context.RequestedCategories.HasFlag(TransportHealthCheckCategory.Performance).ShouldBeTrue();
		context.RequestedCategories.HasFlag(TransportHealthCheckCategory.Resources).ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithNoneCategories_SetsNoneCategories()
	{
		// Arrange & Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.None);

		// Assert
		context.RequestedCategories.ShouldBe(TransportHealthCheckCategory.None);
	}

	#endregion

	#region Timeout Tests

	[Fact]
	public void Constructor_WithNullTimeout_SetsDefaultTimeout()
	{
		// Arrange & Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.All, timeout: null);

		// Assert
		context.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Constructor_WithCustomTimeout_SetsCustomTimeout()
	{
		// Arrange
		var customTimeout = TimeSpan.FromSeconds(60);

		// Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.All, customTimeout);

		// Assert
		context.Timeout.ShouldBe(customTimeout);
	}

	[Fact]
	public void Constructor_WithSmallTimeout_SetsSmallTimeout()
	{
		// Arrange
		var smallTimeout = TimeSpan.FromMilliseconds(500);

		// Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.All, smallTimeout);

		// Assert
		context.Timeout.ShouldBe(smallTimeout);
	}

	[Fact]
	public void Constructor_WithLargeTimeout_SetsLargeTimeout()
	{
		// Arrange
		var largeTimeout = TimeSpan.FromMinutes(5);

		// Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.All, largeTimeout);

		// Assert
		context.Timeout.ShouldBe(largeTimeout);
	}

	[Fact]
	public void Constructor_WithZeroTimeout_SetsZeroTimeout()
	{
		// Arrange & Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.All, TimeSpan.Zero);

		// Assert
		context.Timeout.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region Default Timeout Constant Tests

	[Fact]
	public void DefaultTimeout_Is30Seconds()
	{
		// Arrange & Act
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.All);

		// Assert
		context.Timeout.TotalSeconds.ShouldBe(30);
	}

	#endregion
}
