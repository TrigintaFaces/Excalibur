// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Statistics;

/// <summary>
/// Unit tests for <see cref="TransportPollingStatistics"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class TransportPollingStatisticsShould
{
	[Fact]
	public void HaveZeroTotalPolls_ByDefault()
	{
		// Arrange & Act
		var stats = new TransportPollingStatistics();

		// Assert
		stats.TotalPolls.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalMessages_ByDefault()
	{
		// Arrange & Act
		var stats = new TransportPollingStatistics();

		// Assert
		stats.TotalMessages.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalErrors_ByDefault()
	{
		// Arrange & Act
		var stats = new TransportPollingStatistics();

		// Assert
		stats.TotalErrors.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalDuration_ByDefault()
	{
		// Arrange & Act
		var stats = new TransportPollingStatistics();

		// Assert
		stats.TotalDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowSettingTotalPolls()
	{
		// Arrange
		var stats = new TransportPollingStatistics();

		// Act
		stats.TotalPolls = 100;

		// Assert
		stats.TotalPolls.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingTotalMessages()
	{
		// Arrange
		var stats = new TransportPollingStatistics();

		// Act
		stats.TotalMessages = 5000;

		// Assert
		stats.TotalMessages.ShouldBe(5000);
	}

	[Fact]
	public void AllowSettingTotalErrors()
	{
		// Arrange
		var stats = new TransportPollingStatistics();

		// Act
		stats.TotalErrors = 5;

		// Assert
		stats.TotalErrors.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingTotalDuration()
	{
		// Arrange
		var stats = new TransportPollingStatistics();

		// Act
		stats.TotalDuration = TimeSpan.FromMinutes(10);

		// Assert
		stats.TotalDuration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowLargeValues()
	{
		// Arrange
		var stats = new TransportPollingStatistics();

		// Act
		stats.TotalPolls = int.MaxValue;
		stats.TotalMessages = int.MaxValue;
		stats.TotalErrors = int.MaxValue;
		stats.TotalDuration = TimeSpan.MaxValue;

		// Assert
		stats.TotalPolls.ShouldBe(int.MaxValue);
		stats.TotalMessages.ShouldBe(int.MaxValue);
		stats.TotalErrors.ShouldBe(int.MaxValue);
		stats.TotalDuration.ShouldBe(TimeSpan.MaxValue);
	}
}
