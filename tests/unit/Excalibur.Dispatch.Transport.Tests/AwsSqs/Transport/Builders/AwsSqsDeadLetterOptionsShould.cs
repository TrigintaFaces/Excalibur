// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="AwsSqsDeadLetterOptions"/>.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsDeadLetterOptionsShould : UnitTestBase
{
	private const string ValidQueueArn = "arn:aws:sqs:us-east-1:123456789012:my-dlq";

	#region Constants Tests

	[Fact]
	public void MinMaxReceiveCount_Be1()
	{
		// Assert
		AwsSqsDeadLetterOptions.MinMaxReceiveCount.ShouldBe(1);
	}

	[Fact]
	public void MaxMaxReceiveCount_Be1000()
	{
		// Assert
		AwsSqsDeadLetterOptions.MaxMaxReceiveCount.ShouldBe(1000);
	}

	[Fact]
	public void DefaultMaxReceiveCount_Be5()
	{
		// Assert
		AwsSqsDeadLetterOptions.DefaultMaxReceiveCount.ShouldBe(5);
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Constructor_HaveNullQueueArn()
	{
		// Arrange & Act
		var options = new AwsSqsDeadLetterOptions();

		// Assert
		options.QueueArn.ShouldBeNull();
	}

	[Fact]
	public void Constructor_SetDefaultMaxReceiveCount()
	{
		// Arrange & Act
		var options = new AwsSqsDeadLetterOptions();

		// Assert
		options.MaxReceiveCount.ShouldBe(AwsSqsDeadLetterOptions.DefaultMaxReceiveCount);
	}

	#endregion

	#region QueueArn Tests

	[Fact]
	public void QueueArn_CanBeSetToValidValue()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act
		options.QueueArn = ValidQueueArn;

		// Assert
		options.QueueArn.ShouldBe(ValidQueueArn);
	}

	[Fact]
	public void QueueArn_CanBeSetToNull()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions { QueueArn = ValidQueueArn };

		// Act
		options.QueueArn = null;

		// Assert
		options.QueueArn.ShouldBeNull();
	}

	[Fact]
	public void QueueArn_ThrowWhenSetToEmptyString()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			options.QueueArn = "");
	}

	[Fact]
	public void QueueArn_ThrowWhenSetToWhitespace()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			options.QueueArn = "   ");
	}

	#endregion

	#region MaxReceiveCount Validation Tests

	[Fact]
	public void MaxReceiveCount_AcceptMinimumValue()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act
		options.MaxReceiveCount = AwsSqsDeadLetterOptions.MinMaxReceiveCount;

		// Assert
		options.MaxReceiveCount.ShouldBe(1);
	}

	[Fact]
	public void MaxReceiveCount_AcceptMaximumValue()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act
		options.MaxReceiveCount = AwsSqsDeadLetterOptions.MaxMaxReceiveCount;

		// Assert
		options.MaxReceiveCount.ShouldBe(1000);
	}

	[Fact]
	public void MaxReceiveCount_AcceptValueInRange()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act
		options.MaxReceiveCount = 10;

		// Assert
		options.MaxReceiveCount.ShouldBe(10);
	}

	[Fact]
	public void MaxReceiveCount_ThrowWhenBelowMinimum()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.MaxReceiveCount = 0);
	}

	[Fact]
	public void MaxReceiveCount_ThrowWhenAboveMaximum()
	{
		// Arrange
		var options = new AwsSqsDeadLetterOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.MaxReceiveCount = 1001);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void Options_SupportFullConfiguration()
	{
		// Arrange & Act
		var options = new AwsSqsDeadLetterOptions
		{
			QueueArn = ValidQueueArn,
			MaxReceiveCount = 3,
		};

		// Assert
		options.QueueArn.ShouldBe(ValidQueueArn);
		options.MaxReceiveCount.ShouldBe(3);
	}

	#endregion
}
