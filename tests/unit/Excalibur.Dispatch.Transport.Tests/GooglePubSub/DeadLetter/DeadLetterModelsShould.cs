// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterModelsShould
{
	[Fact]
	public void CreateRetryStrategyWithDefaults()
	{
		// Arrange & Act
		var strategy = new RetryStrategy();

		// Assert
		strategy.MaxRetryAttempts.ShouldBe(0);
		strategy.InitialDelay.ShouldBe(TimeSpan.Zero);
		strategy.MaxDelay.ShouldBe(TimeSpan.Zero);
		strategy.BackoffType.ShouldBe(BackoffType.Constant);
		strategy.JitterEnabled.ShouldBeFalse();
		strategy.CircuitBreakerEnabled.ShouldBeFalse();
		strategy.CircuitBreakerThreshold.ShouldBe(0);
		strategy.CircuitBreakerDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CreateRetryStrategyWithValues()
	{
		// Arrange & Act
		var strategy = new RetryStrategy
		{
			MaxRetryAttempts = 5,
			InitialDelay = TimeSpan.FromSeconds(5),
			MaxDelay = TimeSpan.FromMinutes(5),
			BackoffType = BackoffType.Exponential,
			JitterEnabled = true,
			CircuitBreakerEnabled = true,
			CircuitBreakerThreshold = 3,
			CircuitBreakerDuration = TimeSpan.FromMinutes(1),
		};

		// Assert
		strategy.MaxRetryAttempts.ShouldBe(5);
		strategy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
		strategy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
		strategy.BackoffType.ShouldBe(BackoffType.Exponential);
		strategy.JitterEnabled.ShouldBeTrue();
		strategy.CircuitBreakerEnabled.ShouldBeTrue();
		strategy.CircuitBreakerThreshold.ShouldBe(3);
		strategy.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void CreateFailureRecordWithDefaults()
	{
		// Arrange & Act
		var record = new FailureRecord();

		// Assert
		record.Timestamp.ShouldBe(default);
		record.Exception.ShouldBeNull();
		record.ExceptionType.ShouldBe(string.Empty);
		record.Message.ShouldBe(string.Empty);
		record.StackTraceHash.ShouldBe(string.Empty);
	}

	[Fact]
	public void CreateFailureRecordWithValues()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var ex = new InvalidOperationException("test error");

		// Act
		var record = new FailureRecord
		{
			Timestamp = now,
			Exception = ex,
			ExceptionType = "InvalidOperationException",
			Message = "test error",
			StackTraceHash = "abc123",
		};

		// Assert
		record.Timestamp.ShouldBe(now);
		record.Exception.ShouldBe(ex);
		record.ExceptionType.ShouldBe("InvalidOperationException");
		record.Message.ShouldBe("test error");
		record.StackTraceHash.ShouldBe("abc123");
	}

	[Fact]
	public void CreateRuleDetectionResultWithDefaults()
	{
		// Arrange & Act
		var result = new RuleDetectionResult();

		// Assert
		result.RuleName.ShouldBe(string.Empty);
		result.Confidence.ShouldBe(0);
		result.Reason.ShouldBe(string.Empty);
	}

	[Fact]
	public void CreateRuleDetectionResultWithValues()
	{
		// Arrange & Act
		var result = new RuleDetectionResult
		{
			RuleName = "RapidFailure",
			Confidence = 0.95,
			Reason = "3 failures in 30 seconds",
		};

		// Assert
		result.RuleName.ShouldBe("RapidFailure");
		result.Confidence.ShouldBe(0.95);
		result.Reason.ShouldBe("3 failures in 30 seconds");
	}

	[Fact]
	public void CreateMessageFailureHistory()
	{
		// Arrange & Act
		var history = new MessageFailureHistory("msg-123");

		// Assert
		history.MessageId.ShouldBe("msg-123");
		history.Failures.ShouldBeEmpty();
		history.LastFailureTime.ShouldBe(default);
	}

	[Fact]
	public void TrackFailuresInMessageFailureHistory()
	{
		// Arrange
		var history = new MessageFailureHistory("msg-456");
		var now = DateTimeOffset.UtcNow;

		// Act
		history.Failures.Add(new FailureRecord
		{
			Timestamp = now,
			ExceptionType = "TimeoutException",
			Message = "timed out",
		});
		history.LastFailureTime = now;

		// Assert
		history.Failures.Count.ShouldBe(1);
		history.LastFailureTime.ShouldBe(now);
	}

	[Fact]
	public void CreatePatternInfoWithDefaults()
	{
		// Arrange & Act
		var info = new PatternInfo();

		// Assert
		info.Pattern.ShouldBe(string.Empty);
		info.Occurrences.ShouldBe(0);
		info.LastSeen.ShouldBe(default);
	}

	[Fact]
	public void CreatePatternInfoWithValues()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var info = new PatternInfo
		{
			Pattern = "TimeoutException",
			Occurrences = 5,
			LastSeen = now,
		};

		// Assert
		info.Pattern.ShouldBe("TimeoutException");
		info.Occurrences.ShouldBe(5);
		info.LastSeen.ShouldBe(now);
	}

	[Fact]
	public void CreatePoisonRecommendationWithDefaults()
	{
		// Arrange & Act
		var rec = new PoisonRecommendation();

		// Assert
		rec.Action.ShouldBe(RecommendedAction.Retry);
		rec.Reason.ShouldBe(string.Empty);
		rec.RetryDelay.ShouldBeNull();
		rec.SuggestedFix.ShouldBeNull();
	}

	[Fact]
	public void CreatePoisonRecommendationWithValues()
	{
		// Arrange & Act
		var rec = new PoisonRecommendation
		{
			Action = RecommendedAction.DeadLetter,
			Reason = "Consistent deserialization failure",
			RetryDelay = TimeSpan.FromMinutes(5),
			SuggestedFix = "Check message schema version",
		};

		// Assert
		rec.Action.ShouldBe(RecommendedAction.DeadLetter);
		rec.Reason.ShouldBe("Consistent deserialization failure");
		rec.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		rec.SuggestedFix.ShouldBe("Check message schema version");
	}

	[Fact]
	public void CreatePoisonDetectionResultWithDefaults()
	{
		// Arrange & Act
		var result = new PoisonDetectionResult();

		// Assert
		result.IsPoison.ShouldBeFalse();
		result.MessageId.ShouldBe(string.Empty);
		result.FailureCount.ShouldBe(0);
		result.DetectionResults.ShouldBeEmpty();
		result.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void CreatePoisonDetectionResultWithValues()
	{
		// Arrange & Act
		var result = new PoisonDetectionResult
		{
			IsPoison = true,
			MessageId = "msg-789",
			FailureCount = 10,
			DetectionResults =
			[
				new RuleDetectionResult { RuleName = "RapidFailure", Confidence = 0.9, Reason = "test" },
			],
			Recommendation = new PoisonRecommendation { Action = RecommendedAction.Quarantine },
			Metadata = new Dictionary<string, string> { ["source"] = "test" },
		};

		// Assert
		result.IsPoison.ShouldBeTrue();
		result.MessageId.ShouldBe("msg-789");
		result.FailureCount.ShouldBe(10);
		result.DetectionResults.Count.ShouldBe(1);
		result.Recommendation.Action.ShouldBe(RecommendedAction.Quarantine);
		result.Metadata["source"].ShouldBe("test");
	}
}
