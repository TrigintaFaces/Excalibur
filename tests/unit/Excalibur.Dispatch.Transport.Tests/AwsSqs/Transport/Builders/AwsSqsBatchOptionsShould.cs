// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="AwsSqsBatchOptions"/>.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsBatchOptionsShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void MinBatchSize_Be1()
	{
		// Assert
		AwsSqsBatchOptions.MinBatchSize.ShouldBe(1);
	}

	[Fact]
	public void MaxBatchSize_Be10()
	{
		// Assert
		AwsSqsBatchOptions.MaxBatchSize.ShouldBe(10);
	}

	[Fact]
	public void DefaultSendBatchSize_Be10()
	{
		// Assert
		AwsSqsBatchOptions.DefaultSendBatchSize.ShouldBe(10);
	}

	[Fact]
	public void DefaultReceiveMaxMessages_Be10()
	{
		// Assert
		AwsSqsBatchOptions.DefaultReceiveMaxMessages.ShouldBe(10);
	}

	[Fact]
	public void MinSendBatchWindow_BeZero()
	{
		// Assert
		AwsSqsBatchOptions.MinSendBatchWindow.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void MaxRecommendedSendBatchWindow_Be1Second()
	{
		// Assert
		AwsSqsBatchOptions.MaxRecommendedSendBatchWindow.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void DefaultSendBatchWindow_Be100Milliseconds()
	{
		// Assert
		AwsSqsBatchOptions.DefaultSendBatchWindow.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Constructor_SetDefaultSendBatchSize()
	{
		// Arrange & Act
		var options = new AwsSqsBatchOptions();

		// Assert
		options.SendBatchSize.ShouldBe(AwsSqsBatchOptions.DefaultSendBatchSize);
	}

	[Fact]
	public void Constructor_SetDefaultSendBatchWindow()
	{
		// Arrange & Act
		var options = new AwsSqsBatchOptions();

		// Assert
		options.SendBatchWindow.ShouldBe(AwsSqsBatchOptions.DefaultSendBatchWindow);
	}

	[Fact]
	public void Constructor_SetDefaultReceiveMaxMessages()
	{
		// Arrange & Act
		var options = new AwsSqsBatchOptions();

		// Assert
		options.ReceiveMaxMessages.ShouldBe(AwsSqsBatchOptions.DefaultReceiveMaxMessages);
	}

	#endregion

	#region SendBatchSize Validation Tests

	[Fact]
	public void SendBatchSize_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act
		options.SendBatchSize = AwsSqsBatchOptions.MinBatchSize;

		// Assert
		options.SendBatchSize.ShouldBe(1);
	}

	[Fact]
	public void SendBatchSize_AcceptMaximumValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act
		options.SendBatchSize = AwsSqsBatchOptions.MaxBatchSize;

		// Assert
		options.SendBatchSize.ShouldBe(10);
	}

	[Fact]
	public void SendBatchSize_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act
		options.SendBatchSize = 5;

		// Assert
		options.SendBatchSize.ShouldBe(5);
	}

	[Fact]
	public void SendBatchSize_ThrowWhenBelowMinimum()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.SendBatchSize = 0);
	}

	[Fact]
	public void SendBatchSize_ThrowWhenAboveMaximum()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.SendBatchSize = 11);
	}

	#endregion

	#region SendBatchWindow Validation Tests

	[Fact]
	public void SendBatchWindow_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act
		options.SendBatchWindow = AwsSqsBatchOptions.MinSendBatchWindow;

		// Assert
		options.SendBatchWindow.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void SendBatchWindow_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		var window = TimeSpan.FromMilliseconds(500);

		// Act
		options.SendBatchWindow = window;

		// Assert
		options.SendBatchWindow.ShouldBe(window);
	}

	[Fact]
	public void SendBatchWindow_AcceptValuesAboveRecommendedMax()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();
		var window = TimeSpan.FromSeconds(5);

		// Act
		options.SendBatchWindow = window;

		// Assert - No hard limit, just recommendation
		options.SendBatchWindow.ShouldBe(window);
	}

	[Fact]
	public void SendBatchWindow_ThrowWhenNegative()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.SendBatchWindow = TimeSpan.FromMilliseconds(-1));
	}

	#endregion

	#region ReceiveMaxMessages Validation Tests

	[Fact]
	public void ReceiveMaxMessages_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act
		options.ReceiveMaxMessages = AwsSqsBatchOptions.MinBatchSize;

		// Assert
		options.ReceiveMaxMessages.ShouldBe(1);
	}

	[Fact]
	public void ReceiveMaxMessages_AcceptMaximumValue()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act
		options.ReceiveMaxMessages = AwsSqsBatchOptions.MaxBatchSize;

		// Assert
		options.ReceiveMaxMessages.ShouldBe(10);
	}

	[Fact]
	public void ReceiveMaxMessages_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act
		options.ReceiveMaxMessages = 5;

		// Assert
		options.ReceiveMaxMessages.ShouldBe(5);
	}

	[Fact]
	public void ReceiveMaxMessages_ThrowWhenBelowMinimum()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.ReceiveMaxMessages = 0);
	}

	[Fact]
	public void ReceiveMaxMessages_ThrowWhenAboveMaximum()
	{
		// Arrange
		var options = new AwsSqsBatchOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.ReceiveMaxMessages = 11);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void Options_SupportFullConfiguration()
	{
		// Arrange & Act
		var options = new AwsSqsBatchOptions
		{
			SendBatchSize = 5,
			SendBatchWindow = TimeSpan.FromMilliseconds(200),
			ReceiveMaxMessages = 8,
		};

		// Assert
		options.SendBatchSize.ShouldBe(5);
		options.SendBatchWindow.ShouldBe(TimeSpan.FromMilliseconds(200));
		options.ReceiveMaxMessages.ShouldBe(8);
	}

	[Fact]
	public void Options_SupportMinimalConfiguration()
	{
		// Arrange & Act - Use minimal batch sizes for low-latency processing
		var options = new AwsSqsBatchOptions
		{
			SendBatchSize = 1,
			SendBatchWindow = TimeSpan.Zero,
			ReceiveMaxMessages = 1,
		};

		// Assert
		options.SendBatchSize.ShouldBe(1);
		options.SendBatchWindow.ShouldBe(TimeSpan.Zero);
		options.ReceiveMaxMessages.ShouldBe(1);
	}

	[Fact]
	public void Options_SupportMaxThroughputConfiguration()
	{
		// Arrange & Act - Use max batch sizes for high-throughput processing
		var options = new AwsSqsBatchOptions
		{
			SendBatchSize = 10,
			SendBatchWindow = TimeSpan.FromSeconds(1),
			ReceiveMaxMessages = 10,
		};

		// Assert
		options.SendBatchSize.ShouldBe(10);
		options.SendBatchWindow.ShouldBe(TimeSpan.FromSeconds(1));
		options.ReceiveMaxMessages.ShouldBe(10);
	}

	#endregion
}
