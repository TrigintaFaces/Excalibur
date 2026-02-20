// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="AwsSqsQueueOptions"/>.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsQueueOptionsShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void MinVisibilityTimeout_BeZero()
	{
		// Assert
		AwsSqsQueueOptions.MinVisibilityTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void MaxVisibilityTimeout_Be12Hours()
	{
		// Assert
		AwsSqsQueueOptions.MaxVisibilityTimeout.ShouldBe(TimeSpan.FromHours(12));
	}

	[Fact]
	public void DefaultVisibilityTimeout_Be30Seconds()
	{
		// Assert
		AwsSqsQueueOptions.DefaultVisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void MinMessageRetentionPeriod_Be1Minute()
	{
		// Assert
		AwsSqsQueueOptions.MinMessageRetentionPeriod.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void MaxMessageRetentionPeriod_Be14Days()
	{
		// Assert
		AwsSqsQueueOptions.MaxMessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	public void DefaultMessageRetentionPeriod_Be4Days()
	{
		// Assert
		AwsSqsQueueOptions.DefaultMessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(4));
	}

	[Fact]
	public void MinReceiveWaitTimeSeconds_Be0()
	{
		// Assert
		AwsSqsQueueOptions.MinReceiveWaitTimeSeconds.ShouldBe(0);
	}

	[Fact]
	public void MaxReceiveWaitTimeSeconds_Be20()
	{
		// Assert
		AwsSqsQueueOptions.MaxReceiveWaitTimeSeconds.ShouldBe(20);
	}

	[Fact]
	public void DefaultReceiveWaitTimeSeconds_Be0()
	{
		// Assert
		AwsSqsQueueOptions.DefaultReceiveWaitTimeSeconds.ShouldBe(0);
	}

	[Fact]
	public void MinDelaySeconds_Be0()
	{
		// Assert
		AwsSqsQueueOptions.MinDelaySeconds.ShouldBe(0);
	}

	[Fact]
	public void MaxDelaySeconds_Be900()
	{
		// Assert
		AwsSqsQueueOptions.MaxDelaySeconds.ShouldBe(900);
	}

	[Fact]
	public void DefaultDelaySeconds_Be0()
	{
		// Assert
		AwsSqsQueueOptions.DefaultDelaySeconds.ShouldBe(0);
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Constructor_SetDefaultVisibilityTimeout()
	{
		// Arrange & Act
		var options = new AwsSqsQueueOptions();

		// Assert
		options.VisibilityTimeout.ShouldBe(AwsSqsQueueOptions.DefaultVisibilityTimeout);
	}

	[Fact]
	public void Constructor_SetDefaultMessageRetentionPeriod()
	{
		// Arrange & Act
		var options = new AwsSqsQueueOptions();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(AwsSqsQueueOptions.DefaultMessageRetentionPeriod);
	}

	[Fact]
	public void Constructor_SetDefaultReceiveWaitTimeSeconds()
	{
		// Arrange & Act
		var options = new AwsSqsQueueOptions();

		// Assert
		options.ReceiveWaitTimeSeconds.ShouldBe(AwsSqsQueueOptions.DefaultReceiveWaitTimeSeconds);
	}

	[Fact]
	public void Constructor_SetDefaultDelaySeconds()
	{
		// Arrange & Act
		var options = new AwsSqsQueueOptions();

		// Assert
		options.DelaySeconds.ShouldBe(AwsSqsQueueOptions.DefaultDelaySeconds);
	}

	[Fact]
	public void Constructor_HaveNullDeadLetterQueue()
	{
		// Arrange & Act
		var options = new AwsSqsQueueOptions();

		// Assert
		options.DeadLetterQueue.ShouldBeNull();
		options.HasDeadLetterQueue.ShouldBeFalse();
	}

	#endregion

	#region VisibilityTimeout Validation Tests

	[Fact]
	public void VisibilityTimeout_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.VisibilityTimeout = AwsSqsQueueOptions.MinVisibilityTimeout;

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void VisibilityTimeout_AcceptMaximumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.VisibilityTimeout = AwsSqsQueueOptions.MaxVisibilityTimeout;

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromHours(12));
	}

	[Fact]
	public void VisibilityTimeout_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		var timeout = TimeSpan.FromMinutes(5);

		// Act
		options.VisibilityTimeout = timeout;

		// Assert
		options.VisibilityTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void VisibilityTimeout_ThrowWhenBelowMinimum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.VisibilityTimeout = TimeSpan.FromSeconds(-1));
	}

	[Fact]
	public void VisibilityTimeout_ThrowWhenAboveMaximum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.VisibilityTimeout = TimeSpan.FromHours(13));
	}

	#endregion

	#region MessageRetentionPeriod Validation Tests

	[Fact]
	public void MessageRetentionPeriod_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.MessageRetentionPeriod = AwsSqsQueueOptions.MinMessageRetentionPeriod;

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void MessageRetentionPeriod_AcceptMaximumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.MessageRetentionPeriod = AwsSqsQueueOptions.MaxMessageRetentionPeriod;

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	public void MessageRetentionPeriod_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		var period = TimeSpan.FromDays(7);

		// Act
		options.MessageRetentionPeriod = period;

		// Assert
		options.MessageRetentionPeriod.ShouldBe(period);
	}

	[Fact]
	public void MessageRetentionPeriod_ThrowWhenBelowMinimum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.MessageRetentionPeriod = TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void MessageRetentionPeriod_ThrowWhenAboveMaximum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.MessageRetentionPeriod = TimeSpan.FromDays(15));
	}

	#endregion

	#region ReceiveWaitTimeSeconds Validation Tests

	[Fact]
	public void ReceiveWaitTimeSeconds_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.ReceiveWaitTimeSeconds = AwsSqsQueueOptions.MinReceiveWaitTimeSeconds;

		// Assert
		options.ReceiveWaitTimeSeconds.ShouldBe(0);
	}

	[Fact]
	public void ReceiveWaitTimeSeconds_AcceptMaximumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.ReceiveWaitTimeSeconds = AwsSqsQueueOptions.MaxReceiveWaitTimeSeconds;

		// Assert
		options.ReceiveWaitTimeSeconds.ShouldBe(20);
	}

	[Fact]
	public void ReceiveWaitTimeSeconds_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.ReceiveWaitTimeSeconds = 10;

		// Assert
		options.ReceiveWaitTimeSeconds.ShouldBe(10);
	}

	[Fact]
	public void ReceiveWaitTimeSeconds_ThrowWhenBelowMinimum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.ReceiveWaitTimeSeconds = -1);
	}

	[Fact]
	public void ReceiveWaitTimeSeconds_ThrowWhenAboveMaximum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.ReceiveWaitTimeSeconds = 21);
	}

	#endregion

	#region DelaySeconds Validation Tests

	[Fact]
	public void DelaySeconds_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.DelaySeconds = AwsSqsQueueOptions.MinDelaySeconds;

		// Assert
		options.DelaySeconds.ShouldBe(0);
	}

	[Fact]
	public void DelaySeconds_AcceptMaximumValue()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.DelaySeconds = AwsSqsQueueOptions.MaxDelaySeconds;

		// Assert
		options.DelaySeconds.ShouldBe(900);
	}

	[Fact]
	public void DelaySeconds_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act
		options.DelaySeconds = 300;

		// Assert
		options.DelaySeconds.ShouldBe(300);
	}

	[Fact]
	public void DelaySeconds_ThrowWhenBelowMinimum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.DelaySeconds = -1);
	}

	[Fact]
	public void DelaySeconds_ThrowWhenAboveMaximum()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.DelaySeconds = 901);
	}

	#endregion

	#region DeadLetterQueue Tests

	[Fact]
	public void DeadLetterQueue_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsQueueOptions();
		var dlqOptions = new AwsSqsDeadLetterOptions();

		// Act
		options.DeadLetterQueue = dlqOptions;

		// Assert
		options.DeadLetterQueue.ShouldBeSameAs(dlqOptions);
		options.HasDeadLetterQueue.ShouldBeTrue();
	}

	[Fact]
	public void DeadLetterQueue_CanBeSetToNull()
	{
		// Arrange
		var options = new AwsSqsQueueOptions
		{
			DeadLetterQueue = new AwsSqsDeadLetterOptions(),
		};

		// Act
		options.DeadLetterQueue = null;

		// Assert
		options.DeadLetterQueue.ShouldBeNull();
		options.HasDeadLetterQueue.ShouldBeFalse();
	}

	#endregion
}
