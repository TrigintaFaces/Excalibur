// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Depth coverage tests for <see cref="PublishingResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PublishingResultDepthShould
{
	[Fact]
	public void TotalProcessed_SumsAllCounts()
	{
		// Arrange
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
		// Arrange
		var result = new PublishingResult
		{
			SuccessCount = 8,
			FailureCount = 2,
		};

		// Assert
		result.SuccessRate.ShouldBe(80.0);
	}

	[Fact]
	public void SuccessRate_Returns100_WhenNoMessages()
	{
		// Arrange
		var result = new PublishingResult();

		// Assert
		result.SuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public void SuccessRate_Returns0_WhenAllFailed()
	{
		// Arrange
		var result = new PublishingResult
		{
			SuccessCount = 0,
			FailureCount = 5,
		};

		// Assert
		result.SuccessRate.ShouldBe(0.0);
	}

	[Fact]
	public void IsSuccess_ReturnsTrue_WhenNoFailures()
	{
		// Arrange
		var result = new PublishingResult
		{
			SuccessCount = 10,
			FailureCount = 0,
		};

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void IsSuccess_ReturnsFalse_WhenHasFailures()
	{
		// Arrange
		var result = new PublishingResult
		{
			SuccessCount = 8,
			FailureCount = 2,
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void Success_FactoryMethod_SetsCorrectValues()
	{
		// Act
		var result = PublishingResult.Success(5, 2, TimeSpan.FromSeconds(3));

		// Assert
		result.SuccessCount.ShouldBe(5);
		result.SkippedCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
		result.Duration.ShouldBe(TimeSpan.FromSeconds(3));
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void Success_FactoryMethod_WithDefaults()
	{
		// Act
		var result = PublishingResult.Success(10);

		// Assert
		result.SuccessCount.ShouldBe(10);
		result.SkippedCount.ShouldBe(0);
		result.Duration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void WithFailures_FactoryMethod_SetsCorrectValues()
	{
		// Arrange
		var errors = new List<PublishingError>
		{
			new("msg-1", "Connection timeout"),
			new("msg-2", "Serialization failed"),
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
	public void Errors_DefaultsToEmptyList()
	{
		// Arrange
		var result = new PublishingResult();

		// Assert
		result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Timestamp_DefaultsToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = new PublishingResult();

		// Assert
		var after = DateTimeOffset.UtcNow;
		result.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		result.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void ToString_ContainsSuccessCount()
	{
		// Arrange
		var result = new PublishingResult { SuccessCount = 15 };

		// Assert
		result.ToString().ShouldContain("15 success");
	}

	[Fact]
	public void ToString_ContainsFailedCount()
	{
		// Arrange
		var result = new PublishingResult { FailureCount = 3 };

		// Assert
		result.ToString().ShouldContain("3 failed");
	}

	[Fact]
	public void ToString_ContainsSkippedCount()
	{
		// Arrange
		var result = new PublishingResult { SkippedCount = 7 };

		// Assert
		result.ToString().ShouldContain("7 skipped");
	}

	[Fact]
	public void ToString_ContainsDurationInMs()
	{
		// Arrange
		var result = new PublishingResult { Duration = TimeSpan.FromMilliseconds(250) };

		// Assert
		result.ToString().ShouldContain("ms");
	}
}
