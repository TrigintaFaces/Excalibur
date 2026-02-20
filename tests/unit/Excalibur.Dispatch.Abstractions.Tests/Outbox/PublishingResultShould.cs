// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="PublishingResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PublishingResultShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var result = new PublishingResult();

		// Assert
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
		result.SkippedCount.ShouldBe(0);
		result.Duration.ShouldBe(TimeSpan.Zero);
		result.Errors.ShouldBeEmpty();
		result.IsSuccess.ShouldBeTrue(); // No failures = success
	}

	[Fact]
	public void TotalProcessed_SumsAllCounts()
	{
		// Act
		var result = new PublishingResult
		{
			SuccessCount = 10,
			FailureCount = 2,
			SkippedCount = 3,
		};

		// Assert
		result.TotalProcessed.ShouldBe(15);
	}

	[Fact]
	public void SuccessRate_CalculatesCorrectPercentage()
	{
		// Act
		var result = new PublishingResult
		{
			SuccessCount = 8,
			FailureCount = 2,
		};

		// Assert
		result.SuccessRate.ShouldBe(80.0);
	}

	[Fact]
	public void SuccessRate_Returns100_WhenNoMessagesProcessed()
	{
		// Act
		var result = new PublishingResult();

		// Assert
		result.SuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public void IsSuccess_ReturnsFalse_WhenFailuresExist()
	{
		// Act
		var result = new PublishingResult { FailureCount = 1 };

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void Success_FactoryMethod_CreatesSuccessResult()
	{
		// Act
		var result = PublishingResult.Success(10);

		// Assert
		result.SuccessCount.ShouldBe(10);
		result.FailureCount.ShouldBe(0);
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void Success_FactoryMethod_WithSkippedAndDuration()
	{
		// Act
		var result = PublishingResult.Success(5, skippedCount: 2, duration: TimeSpan.FromSeconds(1));

		// Assert
		result.SuccessCount.ShouldBe(5);
		result.SkippedCount.ShouldBe(2);
		result.Duration.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void WithFailures_FactoryMethod_CreatesFailureResult()
	{
		// Arrange
		var errors = new List<PublishingError>
		{
			new("msg-1", "Timeout"),
			new("msg-2", "Connection lost"),
		};

		// Act
		var result = PublishingResult.WithFailures(8, 2, errors, TimeSpan.FromSeconds(5));

		// Assert
		result.SuccessCount.ShouldBe(8);
		result.FailureCount.ShouldBe(2);
		result.Errors.Count.ShouldBe(2);
		result.Duration.ShouldBe(TimeSpan.FromSeconds(5));
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ToString_ContainsRelevantInfo()
	{
		// Arrange
		var result = new PublishingResult
		{
			SuccessCount = 10,
			FailureCount = 2,
			SkippedCount = 1,
			Duration = TimeSpan.FromMilliseconds(500),
		};

		// Act
		var str = result.ToString();

		// Assert
		str.ShouldContain("10");
		str.ShouldContain("2");
		str.ShouldContain("1");
		str.ShouldContain("500");
	}

	[Fact]
	public void Timestamp_HasDefaultValue()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = new PublishingResult();

		// Assert
		result.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
	}
}
