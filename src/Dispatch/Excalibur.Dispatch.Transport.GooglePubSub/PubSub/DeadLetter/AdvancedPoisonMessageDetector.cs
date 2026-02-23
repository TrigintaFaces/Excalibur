// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

// IPoisonDetectionRule interface is defined in DeadLetterTypes.cs

/// <summary>
/// Advanced poison message detector with pattern matching and intelligent detection rules.
/// </summary>
public sealed partial class AdvancedPoisonMessageDetector : IPoisonMessageDetector, IDisposable
{
	private readonly ILogger<AdvancedPoisonMessageDetector> _logger;
	private readonly IOptions<PoisonDetectionOptions> _options;

	/// <summary>
	/// Message tracking.
	/// </summary>
	private readonly ConcurrentDictionary<string, MessageFailureHistory> _messageHistories;

	private readonly ConcurrentDictionary<string, PatternStatistics> _patternStats;
	private readonly List<IPoisonDetectionRule> _detectionRules;

	/// <summary>
	/// Cleanup and maintenance.
	/// </summary>
	private readonly Timer _cleanupTimer;

	private readonly SemaphoreSlim _cleanupLock;

	/// <summary>
	/// Metrics.
	/// </summary>
	private readonly ConcurrentDictionary<string, int> _detectionReasons;

	private long _totalDetections;
	private long _totalEvaluations;

	/// <summary>
	/// Initializes a new instance of the <see cref="AdvancedPoisonMessageDetector" /> class.
	/// </summary>
	public AdvancedPoisonMessageDetector(
		ILogger<AdvancedPoisonMessageDetector> logger,
		IOptions<PoisonDetectionOptions> options)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? throw new ArgumentNullException(nameof(options));

		_messageHistories = new ConcurrentDictionary<string, MessageFailureHistory>(StringComparer.Ordinal);
		_patternStats = new ConcurrentDictionary<string, PatternStatistics>(StringComparer.Ordinal);
		_detectionRules = [];
		_detectionReasons = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
		_cleanupLock = new SemaphoreSlim(1, 1);

		// Initialize default detection rules
		InitializeDefaultRules();

		// Start cleanup timer
		_cleanupTimer = new Timer(
			_ => _ = CleanupExpiredHistoriesAsync(),
			state: null,
			TimeSpan.FromMinutes(1),
			TimeSpan.FromMinutes(1));
	}

	/// <summary>
	/// Evaluates whether a message is a poison message based on its failure history and patterns.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "cancellationToken reserved for future async operations when detection rules become async")]
	public Task<PoisonDetectionResult> EvaluateMessageAsync(
		PubsubMessage message,
		Exception exception,
		CancellationToken cancellationToken)
	{
		return Task.FromResult(EvaluateMessageCore(message, exception));
	}

	private PoisonDetectionResult EvaluateMessageCore(PubsubMessage message, Exception exception)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("EvaluateMessage");
		_ = activity?.SetTag("message_id", message.MessageId);

		_ = Interlocked.Increment(ref _totalEvaluations);

		// Get or create message history
		var history = _messageHistories.GetOrAdd(
			message.MessageId,
			_ => new MessageFailureHistory(message.MessageId));

		// Record the failure
		var failure = new FailureRecord
		{
			Timestamp = DateTimeOffset.UtcNow,
			Exception = exception,
			ExceptionType = exception.GetType().Name,
			Message = exception.Message,
			StackTraceHash = HashStackTrace(exception.StackTrace),
		};

		history.Failures.Add(failure);
		history.LastFailureTime = failure.Timestamp;

		// Update pattern statistics
		UpdatePatternStatistics(exception, message);

		// Evaluate against all rules
		var detectionResults = new List<RuleDetectionResult>();

		foreach (var rule in _detectionRules)
		{
			try
			{
				if (rule.IsPoison(message, exception, history))
				{
					detectionResults.Add(new RuleDetectionResult
					{
						RuleName = rule.Name,
						Confidence = rule.GetConfidence(message, exception, history),
						Reason = rule.GetReason(message, exception, history),
					});
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error evaluating poison detection rule {Rule}", rule.Name);
			}
		}

		// Determine if message is poison based on results
		var isPoison = DetermineIfPoison(detectionResults, history);

		if (isPoison)
		{
			_ = Interlocked.Increment(ref _totalDetections);
			var primaryReason = detectionResults.OrderByDescending(r => r.Confidence).First().Reason;
			_ = _detectionReasons.AddOrUpdate(primaryReason, 1, (_, count) => count + 1);

			_logger.LogWarning(
				"Poison message detected: {MessageId}, Reason: {Reason}, FailureCount: {Count}",
				message.MessageId,
				primaryReason,
				history.Failures.Count);
		}

		return new PoisonDetectionResult
		{
			IsPoison = isPoison,
			MessageId = message.MessageId,
			FailureCount = history.Failures.Count,
			DetectionResults = detectionResults,
			Recommendation = GetRecommendation(isPoison, history, detectionResults),
			Metadata = new Dictionary<string, string>
				(StringComparer.Ordinal)
			{
				["first_failure"] = history.Failures[0].Timestamp.ToString("O"),
				["last_failure"] = history.LastFailureTime.ToString("O"),
				["unique_exceptions"] =
						history.Failures.Select(f => f.ExceptionType).Distinct(StringComparer.Ordinal).Count().ToString(),
				["pattern_match"] = (detectionResults.Count != 0).ToString(),
			},
		};
	}

	/// <summary>
	/// Registers a custom poison detection rule.
	/// </summary>
	public void RegisterRule(IPoisonDetectionRule rule)
	{
		ArgumentNullException.ThrowIfNull(rule);

		_detectionRules.Add(rule);
		_logger.LogInformation("Registered poison detection rule: {RuleName}", rule.Name);
	}

	/// <summary>
	/// Gets statistics about poison message detection.
	/// </summary>
	public PoisonDetectionStatistics GetStatistics()
	{
		var multipleFailuresCount = _messageHistories.Values.Count(h => h.Failures.Count > 1);
		var totalFailures = _messageHistories.Values.Sum(h => h.Failures.Count);
		var recentCutoff = DateTimeOffset.UtcNow.AddHours(-24);
		var recentFailures = _messageHistories.Values.Count(h => h.LastFailureTime > recentCutoff);

		return new PoisonDetectionStatistics
		{
			TotalTrackedMessages = _messageHistories.Count,
			MultipleFailuresCount = multipleFailuresCount,
			TotalFailures = totalFailures,
			RecentFailures = recentFailures,
			DetectedPatterns =
			[
				.. _patternStats.Values.Select(p =>
					new PatternInfo { Pattern = p.Pattern, Occurrences = p.Occurrences, LastSeen = p.LastSeen }),
			],
		};
	}

	/// <summary>
	/// Clears the history for a specific message.
	/// </summary>
	public void ClearMessageHistory(string messageId) => _ = _messageHistories.TryRemove(messageId, out _);

	/// <summary>
	/// Determines if a message is a poison message.
	/// </summary>
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "IPoisonMessageDetector.IsPoisonMessage() is synchronous by interface contract.")]
	public bool IsPoisonMessage(PubsubMessage message, Exception exception)
	{
		return EvaluateMessageCore(message, exception).IsPoison;
	}

	/// <summary>
	/// Records a processing failure for tracking poison messages.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task RecordFailureAsync(string messageId, Exception exception)
	{
		var history = _messageHistories.GetOrAdd(messageId, _ => new MessageFailureHistory(messageId));
		var failure = new FailureRecord
		{
			Timestamp = DateTimeOffset.UtcNow,
			Exception = exception,
			ExceptionType = exception.GetType().Name,
			Message = exception.Message,
			StackTraceHash = HashStackTrace(exception.StackTrace),
		};
		history.Failures.Add(failure);
		history.LastFailureTime = failure.Timestamp;
		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets the failure count for a message.
	/// </summary>
	public int GetFailureCount(string messageId) => _messageHistories.TryGetValue(messageId, out var history) ? history.Failures.Count : 0;

	/// <summary>
	/// Resets the failure count for a message.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task ResetFailureCountAsync(string messageId)
	{
		ClearMessageHistory(messageId);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_cleanupTimer?.Dispose();
		_cleanupLock?.Dispose();
	}

	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "history parameter reserved for future failure pattern analysis")]
	private static bool DetermineIfPoison(
		List<RuleDetectionResult> detectionResults,
		MessageFailureHistory history)
	{
		if (detectionResults.Count == 0)
		{
			return false;
		}

		// Use weighted scoring based on confidence
		var totalScore = detectionResults.Sum(static r => r.Confidence);
		var avgScore = totalScore / detectionResults.Count;

		// Consider poison if:
		// 1. Any rule has very high confidence (>= 0.9)
		// 2. Average confidence is high (>= 0.7)
		// 3. Multiple rules triggered with medium confidence
		return detectionResults.Exists(static r => r.Confidence >= 0.9) ||
			   avgScore >= 0.7 ||
			   (detectionResults.Count >= 2 && avgScore >= 0.5);
	}

	[SuppressMessage("Security", "CA5394:Do not use insecure randomness",
		Justification = "Random is used for retry jitter timing, not for security purposes. Cryptographic randomness is unnecessary for backoff delays.")]
	private static TimeSpan CalculateRetryDelay(int failureCount)
	{
		// Exponential backoff with jitter
		var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(failureCount - 1, 10)));
		var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
		return baseDelay + jitter;
	}

	private static string GetExceptionPattern(Exception exception)
	{
		// Extract pattern from exception message Remove variable parts like IDs, timestamps, etc.
		var message = exception.Message;

		// Remove GUIDs
		message = MyRegex().Replace(message, "[GUID]");

		// Remove numbers
		message = Regex.Replace(
			message,
			@"\d+",
			"[NUM]");

		// Remove timestamps
		message = Regex.Replace(
			message,
			@"\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}",
			"[TIMESTAMP]");

		return message.Length > 100 ? message.Substring(0, 100) : message;
	}

	private static string HashStackTrace(string? stackTrace)
	{
		if (string.IsNullOrEmpty(stackTrace))
		{
			return "none";
		}

		// Simple hash of stack trace for comparison
		var hash = stackTrace.GetHashCode(StringComparison.Ordinal);
		return hash.ToString("X8");
	}

	[GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
	private static partial Regex MyRegex();

	private void InitializeDefaultRules()
	{
		// Rule 1: Repeated failures threshold
		RegisterRule(new ThresholdRule(
			"RepeatedFailures",
			_options.Value.MaxFailuresBeforePoison));

		// Rule 2: Rapid failure rate
		RegisterRule(new RapidFailureRule(
			"RapidFailures",
			_options.Value.RapidFailureCount,
			_options.Value.RapidFailureWindow));

		// Rule 3: Consistent exception pattern
		RegisterRule(new ConsistentExceptionRule(
			"ConsistentException",
			_options.Value.ConsistentExceptionThreshold));

		// Rule 4: Timeout pattern
		RegisterRule(new TimeoutPatternRule(
			"TimeoutPattern",
			_options.Value.TimeoutThreshold));

		// Rule 5: Memory/Resource exhaustion
		RegisterRule(new ResourceExhaustionRule(
			"ResourceExhaustion"));

		// Rule 6: Deserialization failures
		RegisterRule(new DeserializationFailureRule(
			"DeserializationFailure"));

		// Rule 7: Business logic loop detection
		RegisterRule(new BusinessLogicLoopRule(
			"BusinessLogicLoop",
			_options.Value.LoopDetectionThreshold));
	}

	private PoisonRecommendation GetRecommendation(
		bool isPoison,
		MessageFailureHistory history,
		List<RuleDetectionResult> detectionResults)
	{
		if (!isPoison)
		{
			return new PoisonRecommendation
			{
				Action = RecommendedAction.Retry,
				Reason = "Message is not detected as poison",
				RetryDelay = CalculateRetryDelay(history.Failures.Count),
			};
		}

		// Analyze the detection results
		var primaryResult = detectionResults.OrderByDescending(static r => r.Confidence).First();

		return primaryResult.RuleName switch
		{
			"DeserializationFailure" => new PoisonRecommendation
			{
				Action = RecommendedAction.DeadLetter,
				Reason = "Message has invalid format/schema",
				SuggestedFix = "Validate message schema and update producer",
			},
			"ResourceExhaustion" => new PoisonRecommendation
			{
				Action = RecommendedAction.Quarantine,
				Reason = "Message causes resource exhaustion",
				RetryDelay = TimeSpan.FromHours(1),
				SuggestedFix = "Investigate resource requirements",
			},
			"BusinessLogicLoop" => new PoisonRecommendation
			{
				Action = RecommendedAction.DeadLetter,
				Reason = "Message causes infinite processing loop",
				SuggestedFix = "Fix business logic to handle this case",
			},
			_ => new PoisonRecommendation
			{
				Action = RecommendedAction.DeadLetter,
				Reason = primaryResult.Reason,
				RetryDelay = TimeSpan.FromMinutes(30),
			},
		};
	}

	private void UpdatePatternStatistics(Exception exception, PubsubMessage message)
	{
		// Track exception patterns
		var pattern = $"{exception.GetType().Name}:{GetExceptionPattern(exception)}";
		var stats = _patternStats.GetOrAdd(pattern, _ => new PatternStatistics { Pattern = pattern });

		// Use lock for thread-safe updates since we can't use Interlocked with properties
		lock (stats)
		{
			stats.Occurrences++;
			stats.LastSeen = DateTimeOffset.UtcNow;
		}

		// Track message attribute patterns
		if (message.Attributes != null && message.Attributes.TryGetValue("message_type", out var messageType))
		{
			var msgPattern = $"MessageType:{messageType}";
			var msgStats = _patternStats.GetOrAdd(msgPattern, _ => new PatternStatistics { Pattern = msgPattern });
			lock (msgStats)
			{
				msgStats.Occurrences++;
				msgStats.LastSeen = DateTimeOffset.UtcNow;
			}
		}
	}

	private async Task CleanupExpiredHistoriesAsync()
	{
		await _cleanupLock.WaitAsync().ConfigureAwait(false);
		try
		{
			var cutoff = DateTimeOffset.UtcNow - _options.Value.HistoryRetentionPeriod;
			var toRemove = _messageHistories
				.Where(kvp => kvp.Value.LastFailureTime < cutoff)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var key in toRemove)
			{
				_ = _messageHistories.TryRemove(key, out _);
			}

			if (toRemove.Count > 0)
			{
				_logger.LogDebug("Cleaned up {Count} expired message histories", toRemove.Count);
			}

			// Cleanup old patterns
			var patternCutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(7);
			var oldPatterns = _patternStats
				.Where(kvp => kvp.Value.LastSeen < patternCutoff)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var pattern in oldPatterns)
			{
				_ = _patternStats.TryRemove(pattern, out _);
			}
		}
		finally
		{
			_ = _cleanupLock.Release();
		}
	}
}

// Internal pattern tracking
