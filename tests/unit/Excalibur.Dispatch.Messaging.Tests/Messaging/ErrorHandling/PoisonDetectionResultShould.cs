// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="PoisonDetectionResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
[Trait("Priority", "0")]
public sealed class PoisonDetectionResultShould
{
	#region Default Value Tests

	[Fact]
	public void Default_IsPoison_IsFalse()
	{
		// Arrange & Act
		var result = new PoisonDetectionResult();

		// Assert
		result.IsPoison.ShouldBeFalse();
	}

	[Fact]
	public void Default_Reason_IsNull()
	{
		// Arrange & Act
		var result = new PoisonDetectionResult();

		// Assert
		result.Reason.ShouldBeNull();
	}

	[Fact]
	public void Default_DetectorName_IsNull()
	{
		// Arrange & Act
		var result = new PoisonDetectionResult();

		// Assert
		result.DetectorName.ShouldBeNull();
	}

	[Fact]
	public void Default_Details_IsEmptyDictionary()
	{
		// Arrange & Act
		var result = new PoisonDetectionResult();

		// Assert
		_ = result.Details.ShouldNotBeNull();
		result.Details.ShouldBeEmpty();
	}

	#endregion

	#region Factory Method Tests

	[Fact]
	public void Poison_ReturnsResultWithIsPoisonTrue()
	{
		// Act
		var result = PoisonDetectionResult.Poison("Max retries exceeded", "RetryCountDetector");

		// Assert
		result.IsPoison.ShouldBeTrue();
	}

	[Fact]
	public void Poison_SetsReasonAndDetectorName()
	{
		// Act
		var result = PoisonDetectionResult.Poison("Message expired", "TimespanDetector");

		// Assert
		result.Reason.ShouldBe("Message expired");
		result.DetectorName.ShouldBe("TimespanDetector");
	}

	[Fact]
	public void Poison_WithDetails_SetsDetails()
	{
		// Arrange
		var details = new Dictionary<string, object>
		{
			["retryCount"] = 5,
			["maxRetries"] = 3,
		};

		// Act
		var result = PoisonDetectionResult.Poison("Max retries exceeded", "RetryCountDetector", details);

		// Assert
		result.Details.Count.ShouldBe(2);
		result.Details["retryCount"].ShouldBe(5);
		result.Details["maxRetries"].ShouldBe(3);
	}

	[Fact]
	public void Poison_WithNullDetails_CreatesEmptyDetailsDictionary()
	{
		// Act
		var result = PoisonDetectionResult.Poison("Test reason", "TestDetector", null);

		// Assert
		_ = result.Details.ShouldNotBeNull();
		result.Details.ShouldBeEmpty();
	}

	[Fact]
	public void NotPoison_ReturnsResultWithIsPoisonFalse()
	{
		// Act
		var result = PoisonDetectionResult.NotPoison();

		// Assert
		result.IsPoison.ShouldBeFalse();
	}

	[Fact]
	public void NotPoison_HasNullReasonAndDetectorName()
	{
		// Act
		var result = PoisonDetectionResult.NotPoison();

		// Assert
		result.Reason.ShouldBeNull();
		result.DetectorName.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void IsPoison_CanBeSet()
	{
		// Arrange
		var result = new PoisonDetectionResult();

		// Act
		result.IsPoison = true;

		// Assert
		result.IsPoison.ShouldBeTrue();
	}

	[Fact]
	public void Details_CanAddItems()
	{
		// Arrange
		var result = new PoisonDetectionResult();

		// Act
		result.Details["key1"] = "value1";
		result.Details["key2"] = 42;

		// Assert
		result.Details.Count.ShouldBe(2);
		result.Details["key1"].ShouldBe("value1");
		result.Details["key2"].ShouldBe(42);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Result_ForRetryExceeded_ContainsRelevantDetails()
	{
		// Act
		var result = PoisonDetectionResult.Poison(
			"Exceeded maximum retry count of 3",
			"RetryCountPoisonDetector",
			new Dictionary<string, object>
			{
				["currentRetries"] = 4,
				["maxRetries"] = 3,
				["messageId"] = "msg-123",
			});

		// Assert
		result.IsPoison.ShouldBeTrue();
		result.Details["currentRetries"].ShouldBe(4);
		((int)result.Details["currentRetries"]).ShouldBeGreaterThan((int)result.Details["maxRetries"]);
	}

	[Fact]
	public void Result_ForExpiredMessage_ContainsTimespanDetails()
	{
		// Act
		var result = PoisonDetectionResult.Poison(
			"Message has been processing for too long",
			"TimespanPoisonDetector",
			new Dictionary<string, object>
			{
				["processingDuration"] = TimeSpan.FromHours(2),
				["maxDuration"] = TimeSpan.FromHours(1),
			});

		// Assert
		result.IsPoison.ShouldBeTrue();
		result.DetectorName.ShouldContain("Timespan");
	}

	[Fact]
	public void Result_ForHealthyMessage_HasNoDetails()
	{
		// Act
		var result = PoisonDetectionResult.NotPoison();

		// Assert
		result.IsPoison.ShouldBeFalse();
		result.Details.ShouldBeEmpty();
	}

	#endregion
}
