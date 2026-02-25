// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="AwsSqsFifoOptions"/>.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsFifoOptionsShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void FifoQueueSuffix_BeDotFifo()
	{
		// Assert
		AwsSqsFifoOptions.FifoQueueSuffix.ShouldBe(".fifo");
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Constructor_HaveContentBasedDeduplicationDisabled()
	{
		// Arrange & Act
		var options = new AwsSqsFifoOptions();

		// Assert
		options.ContentBasedDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_HaveNullDeduplicationIdSelector()
	{
		// Arrange & Act
		var options = new AwsSqsFifoOptions();

		// Assert
		options.DeduplicationIdSelector.ShouldBeNull();
	}

	[Fact]
	public void Constructor_HaveNullMessageGroupIdSelector()
	{
		// Arrange & Act
		var options = new AwsSqsFifoOptions();

		// Assert
		options.MessageGroupIdSelector.ShouldBeNull();
	}

	#endregion

	#region ContentBasedDeduplication Tests

	[Fact]
	public void ContentBasedDeduplication_CanBeEnabled()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();

		// Act
		options.ContentBasedDeduplication = true;

		// Assert
		options.ContentBasedDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void ContentBasedDeduplication_CanBeDisabled()
	{
		// Arrange
		var options = new AwsSqsFifoOptions { ContentBasedDeduplication = true };

		// Act
		options.ContentBasedDeduplication = false;

		// Assert
		options.ContentBasedDeduplication.ShouldBeFalse();
	}

	#endregion

	#region DeduplicationIdSelector Tests

	[Fact]
	public void DeduplicationIdSelector_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		Func<object, string> selector = msg => "test-id";

		// Act
		options.DeduplicationIdSelector = selector;

		// Assert
		options.DeduplicationIdSelector.ShouldBeSameAs(selector);
	}

	[Fact]
	public void DeduplicationIdSelector_CanBeSetToNull()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			DeduplicationIdSelector = _ => "test",
		};

		// Act
		options.DeduplicationIdSelector = null;

		// Assert
		options.DeduplicationIdSelector.ShouldBeNull();
	}

	[Fact]
	public void DeduplicationIdSelector_ReturnCorrectValue()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			DeduplicationIdSelector = msg => $"dedup-{msg.GetHashCode()}",
		};
		var testObject = new { Id = 123 };

		// Act
		var result = options.DeduplicationIdSelector(testObject);

		// Assert
		result.ShouldBe($"dedup-{testObject.GetHashCode()}");
	}

	#endregion

	#region MessageGroupIdSelector Tests

	[Fact]
	public void MessageGroupIdSelector_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		Func<object, string> selector = _ => "global";

		// Act
		options.MessageGroupIdSelector = selector;

		// Assert
		options.MessageGroupIdSelector.ShouldBeSameAs(selector);
	}

	[Fact]
	public void MessageGroupIdSelector_CanBeSetToNull()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			MessageGroupIdSelector = _ => "test",
		};

		// Act
		options.MessageGroupIdSelector = null;

		// Assert
		options.MessageGroupIdSelector.ShouldBeNull();
	}

	[Fact]
	public void MessageGroupIdSelector_ReturnCorrectValue()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			MessageGroupIdSelector = _ => "tenant-123",
		};

		// Act
		var result = options.MessageGroupIdSelector(new object());

		// Assert
		result.ShouldBe("tenant-123");
	}

	#endregion

	#region HasValidDeduplication Tests

	[Fact]
	public void HasValidDeduplication_ReturnFalseWhenBothNotConfigured()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();

		// Assert
		options.HasValidDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void HasValidDeduplication_ReturnTrueWhenContentBasedDeduplicationEnabled()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			ContentBasedDeduplication = true,
		};

		// Assert
		options.HasValidDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void HasValidDeduplication_ReturnTrueWhenDeduplicationIdSelectorSet()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			DeduplicationIdSelector = _ => "test",
		};

		// Assert
		options.HasValidDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void HasValidDeduplication_ReturnTrueWhenBothConfigured()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			ContentBasedDeduplication = true,
			DeduplicationIdSelector = _ => "test",
		};

		// Assert
		options.HasValidDeduplication.ShouldBeTrue();
	}

	#endregion

	#region HasMessageGroupIdSelector Tests

	[Fact]
	public void HasMessageGroupIdSelector_ReturnFalseWhenNotConfigured()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();

		// Assert
		options.HasMessageGroupIdSelector.ShouldBeFalse();
	}

	[Fact]
	public void HasMessageGroupIdSelector_ReturnTrueWhenConfigured()
	{
		// Arrange
		var options = new AwsSqsFifoOptions
		{
			MessageGroupIdSelector = _ => "global",
		};

		// Assert
		options.HasMessageGroupIdSelector.ShouldBeTrue();
	}

	#endregion

	#region IsValidFifoQueueUrl Tests

	[Theory]
	[InlineData("https://sqs.us-east-1.amazonaws.com/123456789012/my-queue.fifo", true)]
	[InlineData("https://sqs.us-east-1.amazonaws.com/123456789012/my-queue.FIFO", true)]
	[InlineData("my-queue.fifo", true)]
	[InlineData("https://sqs.us-east-1.amazonaws.com/123456789012/my-queue", false)]
	[InlineData("my-queue", false)]
	[InlineData("", false)]
	[InlineData(null, false)]
	[InlineData("   ", false)]
	public void IsValidFifoQueueUrl_ValidateCorrectly(string? queueUrl, bool expected)
	{
		// Act
		var result = AwsSqsFifoOptions.IsValidFifoQueueUrl(queueUrl);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region EnsureFifoSuffix Tests

	[Fact]
	public void EnsureFifoSuffix_AppendSuffixWhenMissing()
	{
		// Act
		var result = AwsSqsFifoOptions.EnsureFifoSuffix("my-queue");

		// Assert
		result.ShouldBe("my-queue.fifo");
	}

	[Fact]
	public void EnsureFifoSuffix_NotAppendSuffixWhenPresent()
	{
		// Act
		var result = AwsSqsFifoOptions.EnsureFifoSuffix("my-queue.fifo");

		// Assert
		result.ShouldBe("my-queue.fifo");
	}

	[Fact]
	public void EnsureFifoSuffix_HandleCaseInsensitiveSuffix()
	{
		// Act
		var result = AwsSqsFifoOptions.EnsureFifoSuffix("my-queue.FIFO");

		// Assert
		result.ShouldBe("my-queue.FIFO");
	}

	[Fact]
	public void EnsureFifoSuffix_ThrowWhenQueueNameIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			AwsSqsFifoOptions.EnsureFifoSuffix(null!));
	}

	[Fact]
	public void EnsureFifoSuffix_ThrowWhenQueueNameIsEmpty()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			AwsSqsFifoOptions.EnsureFifoSuffix(""));
	}

	[Fact]
	public void EnsureFifoSuffix_ThrowWhenQueueNameIsWhitespace()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			AwsSqsFifoOptions.EnsureFifoSuffix("   "));
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void Options_SupportFullConfigurationWithContentBasedDeduplication()
	{
		// Arrange & Act
		var options = new AwsSqsFifoOptions
		{
			ContentBasedDeduplication = true,
			MessageGroupIdSelector = _ => "tenant-1",
		};

		// Assert
		options.ContentBasedDeduplication.ShouldBeTrue();
		options.HasValidDeduplication.ShouldBeTrue();
		options.HasMessageGroupIdSelector.ShouldBeTrue();
	}

	[Fact]
	public void Options_SupportFullConfigurationWithCustomDeduplication()
	{
		// Arrange & Act
		var options = new AwsSqsFifoOptions
		{
			DeduplicationIdSelector = msg => msg.GetHashCode().ToString(),
			MessageGroupIdSelector = _ => "global",
		};

		// Assert
		options.ContentBasedDeduplication.ShouldBeFalse();
		options.HasValidDeduplication.ShouldBeTrue();
		options.HasMessageGroupIdSelector.ShouldBeTrue();
	}

	#endregion
}
