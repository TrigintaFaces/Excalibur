// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Text.Json;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Service for analyzing dead letter queue messages and providing insights.
/// </summary>
public class DeadLetterAnalyticsService : BackgroundService, IDeadLetterAnalyticsService
{
	private readonly ILogger<DeadLetterAnalyticsService> _logger;
	private readonly DeadLetterAnalyticsOptions _options;
	private readonly SubscriberServiceApiClient _subscriberClient;
	private readonly ConcurrentDictionary<string, DeadLetterAnalytics> _analytics = new(StringComparer.Ordinal);
	private readonly Timer _reportingTimer;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeadLetterAnalyticsService" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="options"> The analytics options. </param>
	/// <param name="subscriberClient"> The Pub/Sub subscriber API client. </param>
	public DeadLetterAnalyticsService(
		ILogger<DeadLetterAnalyticsService> logger,
		IOptions<DeadLetterAnalyticsOptions> options,
		SubscriberServiceApiClient subscriberClient)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_subscriberClient = subscriberClient ?? throw new ArgumentNullException(nameof(subscriberClient));

		_reportingTimer = new Timer(
			GenerateReport,
			state: null,
			_options.ReportingInterval,
			_options.ReportingInterval);
	}

	/// <summary>
	/// Analyzes a dead letter message and updates analytics.
	/// </summary>
	/// <param name="message"> The dead letter message to analyze. </param>
	/// <param name="cancellationToken"> The cancellation token (reserved for future async processing). </param>
	/// <returns> A task representing the analysis operation. </returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "cancellationToken reserved for future async analytics operations")]
	public Task AnalyzeDeadLetterMessageAsync(PubsubMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var messageType = ExtractMessageType(message);
			var errorReason = ExtractErrorReason(message);
			var originalTopic = ExtractOriginalTopic(message);

			var analytics = _analytics.GetOrAdd(
				messageType,
				_ => new DeadLetterAnalytics { MessageType = messageType, FirstSeen = DateTimeOffset.UtcNow });

			analytics.TotalMessages++;
			analytics.LastSeen = DateTimeOffset.UtcNow;

			if (!string.IsNullOrEmpty(errorReason) && !analytics.ErrorReasons.TryAdd(errorReason, 1))
			{
				analytics.ErrorReasons[errorReason]++;
			}

			if (!string.IsNullOrEmpty(originalTopic) && !analytics.OriginalTopics.TryAdd(originalTopic, 1))
			{
				analytics.OriginalTopics[originalTopic]++;
			}

			// Update hourly statistics
			var hour = DateTimeOffset.UtcNow.Hour;
			if (!analytics.HourlyDistribution.TryAdd(hour, 1))
			{
				analytics.HourlyDistribution[hour]++;
			}

			_logger.LogDebug(
				"Analyzed dead letter message: Type={MessageType}, Error={ErrorReason}",
				messageType, errorReason);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error analyzing dead letter message {MessageId}", message.MessageId);
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets analytics for all message types.
	/// </summary>
	/// <returns> A dictionary of analytics by message type. </returns>
	public IReadOnlyDictionary<string, DeadLetterAnalytics> GetAnalytics() => new Dictionary<string, DeadLetterAnalytics>(_analytics, StringComparer.Ordinal);

	/// <summary>
	/// Gets analytics for a specific message type.
	/// </summary>
	/// <param name="messageType"> The message type. </param>
	/// <returns> Analytics for the message type, or null if not found. </returns>
	public DeadLetterAnalytics? GetAnalytics(string messageType) =>
		_analytics.GetValueOrDefault(messageType);

	/// <summary>
	/// Clears analytics data.
	/// </summary>
	public void ClearAnalytics()
	{
		_analytics.Clear();
		_logger.LogInformation("Dead letter analytics data cleared");
	}

	/// <summary>
	/// Disposes the service resources.
	/// </summary>
	public override void Dispose()
	{
		_reportingTimer?.Dispose();
		base.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public Task RecordDeadLetterAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		if (!_options.Enabled)
		{
			return Task.CompletedTask;
		}

		var analytics = _analytics.GetOrAdd("default", static _ => new DeadLetterAnalytics());
		analytics.TotalDeadLetters++;
		analytics.LastUpdated = DateTimeOffset.UtcNow;

		_logger.LogDebug("Recorded dead letter: {MessageId}, Reason: {Reason}", messageId, reason);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<DeadLetterAnalytics> GetAnalyticsAsync(CancellationToken cancellationToken)
	{
		var analytics = _analytics.GetValueOrDefault("default") ?? new DeadLetterAnalytics();
		return Task.FromResult(analytics);
	}

	/// <summary>
	/// Executes the background service.
	/// </summary>
	/// <param name="stoppingToken"> The cancellation token. </param>
	/// <returns> A task representing the background work. </returns>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Dead Letter Analytics Service started");

		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				await CollectAnalyticsAsync(stoppingToken).ConfigureAwait(false);
				await Task.Delay(_options.CollectionInterval, stoppingToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in Dead Letter Analytics Service");
			throw;
		}
		finally
		{
			_logger.LogInformation("Dead Letter Analytics Service stopped");
		}
	}

	private static string ExtractMessageType(PubsubMessage message)
	{
		if (message.Attributes.TryGetValue("MessageType", out var messageType))
		{
			return messageType;
		}

		if (message.Attributes.TryGetValue("message_type", out var messageTypeLower))
		{
			return messageTypeLower;
		}

		if (message.Attributes.TryGetValue("Type", out var type))
		{
			return type;
		}

		// Try to extract from data if JSON
		try
		{
			var json = JsonDocument.Parse(message.Data.ToByteArray());
			if (json.RootElement.TryGetProperty("type", out var typeProperty))
			{
				return typeProperty.GetString() ?? "unknown";
			}

			if (json.RootElement.TryGetProperty("messageType", out var messageTypeProperty))
			{
				return messageTypeProperty.GetString() ?? "unknown";
			}
		}
		catch
		{
			// Ignore JSON parsing errors
		}

		return "unknown";
	}

	private static string ExtractErrorReason(PubsubMessage message) =>
		message.Attributes.TryGetValue("ErrorReason", out var errorReason)
			? errorReason
			: message.Attributes.TryGetValue("error_reason", out var errorReasonLower)
				? errorReasonLower
				: message.Attributes.GetValueOrDefault("Exception", "unknown");

	private static string ExtractOriginalTopic(PubsubMessage message) =>
		message.Attributes.TryGetValue("OriginalTopic", out var originalTopic)
			? originalTopic
			: message.Attributes.GetValueOrDefault("original_topic", "unknown");

	private async Task CollectAnalyticsAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_options.DeadLetterSubscription == null)
			{
				_logger.LogDebug("No dead letter subscription configured for analytics");
				return;
			}

			var pullRequest = new PullRequest
			{
				SubscriptionAsSubscriptionName = _options.DeadLetterSubscription,
				MaxMessages = _options.BatchSize,
			};

			var response = await _subscriberClient.PullAsync(pullRequest, cancellationToken).ConfigureAwait(false);

			if (response.ReceivedMessages.Count > 0)
			{
				var tasks = response.ReceivedMessages.Select(async receivedMessage =>
				{
					await AnalyzeDeadLetterMessageAsync(receivedMessage.Message, cancellationToken).ConfigureAwait(false);

					// Acknowledge the message so it's not processed again
					await _subscriberClient.AcknowledgeAsync(
						_options.DeadLetterSubscription,
						new[] { receivedMessage.AckId },
						cancellationToken).ConfigureAwait(false);
				});

				await Task.WhenAll(tasks).ConfigureAwait(false);

				_logger.LogDebug(
					"Processed {MessageCount} dead letter messages for analytics",
					response.ReceivedMessages.Count);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error collecting dead letter analytics");
		}
	}

	private void GenerateReport(object? state)
	{
		try
		{
			if (_analytics.IsEmpty)
			{
				_logger.LogDebug("No dead letter analytics data to report");
				return;
			}

			var totalMessages = _analytics.Values.Sum(static a => a.TotalMessages);
			var messageTypes = _analytics.Count;
			var topErrors = _analytics.Values
				.SelectMany(static a => a.ErrorReasons)
				.GroupBy(static kvp => kvp.Key, StringComparer.Ordinal)
				.Select(static g => new { Error = g.Key, Count = g.Sum(static kvp => kvp.Value) })
				.OrderByDescending(static e => e.Count)
				.Take(5)
				.ToList();

			_logger.LogInformation(
				"Dead Letter Analytics Report: Total Messages={TotalMessages}, Message Types={MessageTypes}, Top Errors={TopErrors}",
				totalMessages,
				messageTypes,
				string.Join(", ", topErrors.Select(static e => $"{e.Error}({e.Count})")));

			// Log detailed analytics for each message type
			foreach (var (messageType, analytics) in _analytics.Take(10)) // Limit to top 10 to avoid log spam
			{
				_logger.LogDebug(
					"Message Type Analytics: Type={MessageType}, Count={Count}, First Seen={FirstSeen}, Last Seen={LastSeen}",
					messageType,
					analytics.TotalMessages,
					analytics.FirstSeen,
					analytics.LastSeen);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating dead letter analytics report");
		}
	}
}
