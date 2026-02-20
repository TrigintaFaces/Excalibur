// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

/// <summary>
/// Unit tests for <see cref="DeadLetterAlertThresholds"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class DeadLetterAlertThresholdsShould
{
	[Fact]
	public void Have1000MessageCountThreshold_ByDefault()
	{
		// Arrange & Act
		var thresholds = new DeadLetterAlertThresholds();

		// Assert
		thresholds.MessageCountThreshold.ShouldBe(1000);
	}

	[Fact]
	public void Have7DaysOldestMessageAgeThreshold_ByDefault()
	{
		// Arrange & Act
		var thresholds = new DeadLetterAlertThresholds();

		// Assert
		thresholds.OldestMessageAgeThreshold.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void Have512MBQueueSizeThreshold_ByDefault()
	{
		// Arrange & Act
		var thresholds = new DeadLetterAlertThresholds();

		// Assert
		thresholds.QueueSizeThresholdInBytes.ShouldBe(536_870_912L); // 512 MB
	}

	[Fact]
	public void Have10PercentFailureRateThreshold_ByDefault()
	{
		// Arrange & Act
		var thresholds = new DeadLetterAlertThresholds();

		// Assert
		thresholds.FailureRateThreshold.ShouldBe(10.0);
	}

	[Fact]
	public void AllowSettingMessageCountThreshold()
	{
		// Arrange
		var thresholds = new DeadLetterAlertThresholds();

		// Act
		thresholds.MessageCountThreshold = 5000;

		// Assert
		thresholds.MessageCountThreshold.ShouldBe(5000);
	}

	[Fact]
	public void AllowSettingOldestMessageAgeThreshold()
	{
		// Arrange
		var thresholds = new DeadLetterAlertThresholds();

		// Act
		thresholds.OldestMessageAgeThreshold = TimeSpan.FromDays(30);

		// Assert
		thresholds.OldestMessageAgeThreshold.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void AllowSettingQueueSizeThresholdInBytes()
	{
		// Arrange
		var thresholds = new DeadLetterAlertThresholds();

		// Act
		thresholds.QueueSizeThresholdInBytes = 1_073_741_824L; // 1 GB

		// Assert
		thresholds.QueueSizeThresholdInBytes.ShouldBe(1_073_741_824L);
	}

	[Fact]
	public void AllowSettingFailureRateThreshold()
	{
		// Arrange
		var thresholds = new DeadLetterAlertThresholds();

		// Act
		thresholds.FailureRateThreshold = 5.0;

		// Assert
		thresholds.FailureRateThreshold.ShouldBe(5.0);
	}

	[Fact]
	public void AllowZeroMessageCountThreshold()
	{
		// Arrange
		var thresholds = new DeadLetterAlertThresholds();

		// Act
		thresholds.MessageCountThreshold = 0;

		// Assert
		thresholds.MessageCountThreshold.ShouldBe(0);
	}

	[Fact]
	public void AllowZeroFailureRateThreshold()
	{
		// Arrange
		var thresholds = new DeadLetterAlertThresholds();

		// Act
		thresholds.FailureRateThreshold = 0.0;

		// Assert
		thresholds.FailureRateThreshold.ShouldBe(0.0);
	}

	[Fact]
	public void AllowZeroOldestMessageAgeThreshold()
	{
		// Arrange
		var thresholds = new DeadLetterAlertThresholds();

		// Act
		thresholds.OldestMessageAgeThreshold = TimeSpan.Zero;

		// Assert
		thresholds.OldestMessageAgeThreshold.ShouldBe(TimeSpan.Zero);
	}
}
