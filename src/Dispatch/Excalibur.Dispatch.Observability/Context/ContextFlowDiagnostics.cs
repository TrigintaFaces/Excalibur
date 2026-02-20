// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Diagnostic tools for context flow visualization, debugging helpers, context history tracking, and anomaly detection.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ContextFlowDiagnostics" /> class. </remarks>
/// <param name="logger"> Logger for diagnostic output. </param>
/// <param name="tracker"> Context flow tracker. </param>
/// <param name="metrics"> Metrics collector. </param>
/// <param name="options"> Configuration options. </param>
public sealed partial class ContextFlowDiagnostics(
	ILogger<ContextFlowDiagnostics> logger,
	IContextFlowTracker tracker,
	IContextFlowMetrics metrics,
	IOptions<ContextObservabilityOptions> options) : IContextFlowDiagnostics, IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private static readonly string[] KeyFieldNames = ["MessageId", "CorrelationId", "CausationId", "TenantId", "UserId"];
	private static readonly string[] PiiPatterns =
	[
		"SSN", "SOCIAL", "SECURITY", "CREDIT", "CARD", "PASSWORD", "PWD", "PASSWD", "EMAIL", "MAIL", "PHONE", "MOBILE", "BIRTH",
		"DOB", "ACCOUNT", "ACCT",
	];
	private readonly ILogger<ContextFlowDiagnostics> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IContextFlowTracker _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
	private readonly IContextFlowMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
	private readonly ContextObservabilityOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	private readonly ConcurrentDictionary<string, ContextHistory> _contextHistories =
		new(StringComparer.Ordinal);

#if NET9_0_OR_GREATER
	private readonly Lock _historyEventsLock = new();
#else
	private readonly object _historyEventsLock = new();
#endif
	private readonly ConcurrentQueue<ContextAnomaly> _anomalies = new();
	private volatile int _anomalyCount;

	/// <summary>
	/// Generates a visualization of context flow through the pipeline.
	/// </summary>
	/// <param name="messageId"> The message ID to visualize. </param>
	/// <returns> A string representation of the context flow. </returns>
	[RequiresDynamicCode("Calls CompareSnapshots which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls CompareSnapshots which uses JSON serialization that cannot be statically analyzed")]
	public string VisualizeContextFlow(string messageId)
	{
		var snapshots = _tracker.GetMessageSnapshots(messageId).ToList();
		if (snapshots.Count == 0)
		{
			return string.Create(CultureInfo.InvariantCulture, $"No context flow data available for message {messageId}");
		}

		var sb = new StringBuilder();
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Context Flow Visualization for Message: {messageId}"));
		_ = sb.AppendLine(new string('=', 60));

		ContextSnapshot? previousSnapshot = null;

		foreach (var snapshot in snapshots)
		{
			_ = sb.AppendLine();
			_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Stage: {snapshot.Stage}"));
			_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Time: {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss.fff}"));
			_ = sb.AppendLine(string.Create(
				CultureInfo.InvariantCulture,
				$"Fields: {snapshot.FieldCount} | Size: {snapshot.SizeBytes} bytes"));

			if (previousSnapshot != null)
			{
				var changes = CompareSnapshots(previousSnapshot, snapshot);
				if (changes.Count != 0)
				{
					_ = sb.AppendLine("Changes from previous stage:");
					foreach (var change in changes)
					{
						_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" - {change}"));
					}
				}
			}

			// Show key field values
			_ = sb.AppendLine("Key Fields:");
			foreach (var field in KeyFieldNames)
			{
				if (snapshot.Fields.TryGetValue(field, out var value) && value != null)
				{
					_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" {field}: {FormatFieldValue(value)}"));
				}
			}

			previousSnapshot = snapshot;
		}

		// Add summary
		_ = sb.AppendLine();
		_ = sb.AppendLine(new string('-', 60));
		_ = sb.AppendLine("Summary:");
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" Total Stages: {snapshots.Count}"));
		_ = sb.AppendLine(string.Create(
			CultureInfo.InvariantCulture,
			$" Duration: {(snapshots[^1].Timestamp - snapshots[0].Timestamp).TotalMilliseconds:F2}ms"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" Final Field Count: {snapshots[^1].FieldCount}"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" Final Size: {snapshots[^1].SizeBytes} bytes"));

		return sb.ToString();
	}

	/// <summary>
	/// Analyzes context flow for potential issues.
	/// </summary>
	/// <param name="context"> The message context to analyze. </param>
	/// <returns> Collection of detected issues. </returns>
	[RequiresDynamicCode("Calls EstimateContextSize which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls EstimateContextSize which uses JSON serialization that cannot be statically analyzed")]
	public IEnumerable<ContextDiagnosticIssue> AnalyzeContextHealth(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var issues = new List<ContextDiagnosticIssue>();

		CheckMissingRequiredFields(context, issues);
		CheckOversizedContext(context, issues);
		CheckStaleTimestamps(context, issues);
		CheckHighDeliveryCount(context, issues);
		CheckValidationFailures(context, issues);
		CheckAuthorizationFailures(context, issues);

		return issues;
	}

	/// <summary>
	/// Tracks the history of a context through its lifecycle.
	/// </summary>
	/// <param name="context"> The message context to track. </param>
	/// <param name="eventType"> Type of event to record. </param>
	/// <param name="details"> Event details. </param>
	[RequiresDynamicCode("Calls EstimateContextSize which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls EstimateContextSize which uses JSON serialization that cannot be statically analyzed")]
	public void TrackContextHistory(IMessageContext context, string eventType, string? details = null)
	{
		ArgumentNullException.ThrowIfNull(context);

		var messageId = context.MessageId ?? Guid.NewGuid().ToString();
		var correlationId = context.CorrelationId;

		var history = _contextHistories.AddOrUpdate(
			messageId,
			static (id, arg) => new ContextHistory
			{
				MessageId = id,
				CorrelationId = arg,
				StartTime = DateTimeOffset.UtcNow,
				Events = [],
			},
			static (_, existing, _) => existing,
			correlationId);

		var historyEvent = new ContextHistoryEvent
		{
			Timestamp = DateTimeOffset.UtcNow,
			EventType = eventType,
			Details = details,
			Stage = context.GetItem<string>("PipelineStage"),
			ThreadId = Environment.CurrentManagedThreadId,
			FieldCount = CountContextFields(context),
			SizeBytes = EstimateContextSize(context),
		};

		lock (_historyEventsLock)
		{
			history.Events.Add(historyEvent);

			// Limit history size
			if (history.Events.Count > _options.Limits.MaxHistoryEventsPerContext)
			{
				history.Events.RemoveAt(0);
			}
		}

		LogHistoryEvent(LogLevel.Debug, eventType, messageId, details ?? string.Empty);
	}

	/// <summary>
	/// Detects anomalies in context flow patterns.
	/// </summary>
	/// <param name="context"> The message context to check. </param>
	/// <returns> Detected anomalies. </returns>
	[RequiresDynamicCode("Calls CheckOversizedItem which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls CheckOversizedItem which uses JSON serialization that cannot be statically analyzed")]
	public IEnumerable<ContextAnomaly> DetectAnomalies(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var anomalies = new List<ContextAnomaly>();
		var messageId = context.MessageId ?? "unknown";

		var missingCorrelation = CheckMissingCorrelation(context, messageId);
		if (missingCorrelation != null)
		{
			anomalies.Add(missingCorrelation);
			RecordAnomaly(missingCorrelation);
		}

		var fieldCount = CountContextFields(context);
		var insufficientContext = CheckInsufficientContext(fieldCount, messageId);
		if (insufficientContext != null)
		{
			anomalies.Add(insufficientContext);
			RecordAnomaly(insufficientContext);
		}

		var excessiveContext = CheckExcessiveContext(fieldCount, messageId);
		if (excessiveContext != null)
		{
			anomalies.Add(excessiveContext);
			RecordAnomaly(excessiveContext);
		}

		var circularCausation = CheckCircularCausation(context, messageId);
		if (circularCausation != null)
		{
			anomalies.Add(circularCausation);
			RecordAnomaly(circularCausation);
		}

		foreach (var item in context.Items)
		{
			var piiAnomaly = CheckPotentialPII(item, messageId);
			if (piiAnomaly != null)
			{
				anomalies.Add(piiAnomaly);
				RecordAnomaly(piiAnomaly);
			}

			var oversizedAnomaly = CheckOversizedItem(item, messageId);
			if (oversizedAnomaly != null)
			{
				anomalies.Add(oversizedAnomaly);
				RecordAnomaly(oversizedAnomaly);
			}
		}

		return anomalies;
	}

	/// <summary>
	/// Gets the context history for a specific message.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <returns> The context history if available. </returns>
	public ContextHistory? GetContextHistory(string messageId) =>
		_contextHistories.GetValueOrDefault(messageId);

	/// <summary>
	/// Gets recent anomalies detected in context flow.
	/// </summary>
	/// <param name="limit"> Maximum number of anomalies to return. </param>
	/// <returns> Collection of recent anomalies. </returns>
	public IEnumerable<ContextAnomaly> GetRecentAnomalies(int limit = 100) => _anomalies.TakeLast(limit);

	/// <summary>
	/// Generates a diagnostic report for a correlation chain.
	/// </summary>
	/// <param name="correlationId"> The correlation ID to report on. </param>
	/// <returns> A diagnostic report string. </returns>
	public string GenerateCorrelationReport(string correlationId)
	{
		var lineage = _tracker.GetContextLineage(correlationId);
		if (lineage == null)
		{
			return string.Create(CultureInfo.InvariantCulture, $"No lineage data available for correlation {correlationId}");
		}

		var sb = new StringBuilder();
		AppendReportHeader(sb, correlationId, lineage);
		AppendStageProgression(sb, lineage);
		AppendServiceBoundaries(sb, lineage);
		AppendStatistics(sb, lineage);

		return sb.ToString();
	}

	/// <summary>
	/// Exports diagnostic data for external analysis.
	/// </summary>
	/// <param name="messageId"> Optional message ID to filter by. </param>
	/// <returns> JSON string containing diagnostic data. </returns>
	[RequiresUnreferencedCode(
		"JSON serialization uses reflection which may reference types not preserved during trimming. Ensure serialized types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"JSON serialization for diagnostic data requires dynamic code generation for property reflection and value conversion.")]
	public string ExportDiagnosticData(string? messageId = null)
	{
		var data = new
		{
			Timestamp = DateTimeOffset.UtcNow,
			MessageId = messageId,
			Histories = messageId != null && _contextHistories.TryGetValue(messageId, out var history)
				? [history]
				: _contextHistories.Values.Take(100).ToArray(),
			RecentAnomalies = GetRecentAnomalies(50).ToArray(),
			MetricsSummary = _metrics.GetMetricsSummary(),
		};

		return JsonSerializer.Serialize(data, JsonOptions);
	}

	/// <summary>
	/// Releases all resources used by the <see cref="ContextFlowDiagnostics" />.
	/// </summary>
	/// <remarks>
	/// This implementation is empty because all dependencies are injected and managed by the DI container. The class does not own any
	/// disposable resources.
	/// </remarks>
	public void Dispose()
	{
		// No resources to dispose - all dependencies are managed by DI container
	}

	private static void CheckStaleTimestamps(IMessageContext context, List<ContextDiagnosticIssue> issues)
	{
		if (context.SentTimestampUtc.HasValue)
		{
			var age = DateTimeOffset.UtcNow - context.SentTimestampUtc.Value;
			if (age > TimeSpan.FromHours(1))
			{
				issues.Add(new ContextDiagnosticIssue
				{
					Severity = DiagnosticSeverity.Warning,
					Category = "StaleMessage",
					Description = $"Message is {age.TotalMinutes.ToString("F0", CultureInfo.InvariantCulture)} minutes old",
					Field = "SentTimestampUtc",
					Recommendation = "Investigate processing delays",
				});
			}
		}
	}

	private static double CalculatePreservationRate(ContextLineage lineage)
	{
		if (lineage.ServiceBoundaries.Count == 0)
		{
			return 1.0;
		}

		var preserved = lineage.ServiceBoundaries.Count(static b => b.ContextPreserved);
		return (double)preserved / lineage.ServiceBoundaries.Count;
	}

	private static void CheckHighDeliveryCount(IMessageContext context, List<ContextDiagnosticIssue> issues)
	{
		if (context.DeliveryCount > 5)
		{
			issues.Add(new ContextDiagnosticIssue
			{
				Severity = DiagnosticSeverity.Warning,
				Category = "HighDeliveryCount",
				Description = $"Message has been delivered {context.DeliveryCount.ToString(CultureInfo.InvariantCulture)} times",
				Field = "DeliveryCount",
				Recommendation = "Check for processing failures or poison messages",
			});
		}
	}

	private static void CheckValidationFailures(IMessageContext context, List<ContextDiagnosticIssue> issues)
	{
		if (context.ValidationResult() is IValidationResult { IsValid: false })
		{
			issues.Add(new ContextDiagnosticIssue
			{
				Severity = DiagnosticSeverity.Error,
				Category = "ValidationFailure",
				Description = "Message validation failed",
				Field = "ValidationResult",
				Recommendation = "Review validation errors and fix message content",
			});
		}
	}

	private static void CheckAuthorizationFailures(IMessageContext context, List<ContextDiagnosticIssue> issues)
	{
		if (context.AuthorizationResult() is IAuthorizationResult { IsAuthorized: false })
		{
			issues.Add(new ContextDiagnosticIssue
			{
				Severity = DiagnosticSeverity.Error,
				Category = "AuthorizationFailure",
				Description = "Message authorization failed",
				Field = "AuthorizationResult",
				Recommendation = "Check user permissions and authorization policies",
			});
		}
	}

	private static ContextAnomaly? CheckMissingCorrelation(IMessageContext context, string messageId)
	{
		if (context is { DeliveryCount: > 1, CorrelationId: null })
		{
			return new ContextAnomaly
			{
				Type = AnomalyType.MissingCorrelation,
				Severity = AnomalySeverity.Medium,
				Description = "Message being redelivered without correlation ID",
				MessageId = messageId,
				DetectedAt = DateTimeOffset.UtcNow,
				SuggestedAction = "Add correlation ID for message tracking",
			};
		}

		return null;
	}

	private static ContextAnomaly? CheckInsufficientContext(int fieldCount, string messageId)
	{
		if (fieldCount < 5)
		{
			return new ContextAnomaly
			{
				Type = AnomalyType.InsufficientContext,
				Severity = AnomalySeverity.Low,
				Description = $"Context has unusually few fields ({fieldCount.ToString(CultureInfo.InvariantCulture)})",
				MessageId = messageId,
				DetectedAt = DateTimeOffset.UtcNow,
				SuggestedAction = "Verify context population logic",
			};
		}

		return null;
	}

	private static ContextAnomaly? CheckExcessiveContext(int fieldCount, string messageId)
	{
		if (fieldCount > 100)
		{
			return new ContextAnomaly
			{
				Type = AnomalyType.ExcessiveContext,
				Severity = AnomalySeverity.Medium,
				Description = $"Context has excessive fields ({fieldCount.ToString(CultureInfo.InvariantCulture)})",
				MessageId = messageId,
				DetectedAt = DateTimeOffset.UtcNow,
				SuggestedAction = "Review if all fields are necessary",
			};
		}

		return null;
	}

	private static ContextAnomaly? CheckCircularCausation(IMessageContext context, string messageId)
	{
		if (string.Equals(context.CausationId, context.MessageId, StringComparison.Ordinal))
		{
			return new ContextAnomaly
			{
				Type = AnomalyType.CircularCausation,
				Severity = AnomalySeverity.High,
				Description = "Message has circular causation reference",
				MessageId = messageId,
				DetectedAt = DateTimeOffset.UtcNow,
				SuggestedAction = "Fix causation chain logic",
			};
		}

		return null;
	}

	private static ContextAnomaly? CheckPotentialPII(KeyValuePair<string, object> item, string messageId)
	{
		if (ContainsPotentialPII(item.Key))
		{
			return new ContextAnomaly
			{
				Type = AnomalyType.PotentialPII,
				Severity = AnomalySeverity.High,
				Description = $"Potential PII detected in context item key: {item.Key}",
				MessageId = messageId,
				DetectedAt = DateTimeOffset.UtcNow,
				SuggestedAction = "Remove or encrypt sensitive data",
			};
		}

		return null;
	}

	[RequiresDynamicCode("Calls EstimateItemSize which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls EstimateItemSize which uses JSON serialization that cannot be statically analyzed")]
	private static ContextAnomaly? CheckOversizedItem(KeyValuePair<string, object> item, string messageId)
	{
		var itemSize = EstimateItemSize(item.Value);

		// 10KB per item
		if (itemSize > 10000)
		{
			return new ContextAnomaly
			{
				Type = AnomalyType.OversizedItem,
				Severity = AnomalySeverity.Medium,
				Description = $"Context item '{item.Key}' is unusually large ({itemSize.ToString(CultureInfo.InvariantCulture)} bytes)",
				MessageId = messageId,
				DetectedAt = DateTimeOffset.UtcNow,
				SuggestedAction = "Consider storing large data externally",
			};
		}

		return null;
	}

	private static void AppendReportHeader(StringBuilder sb, string correlationId, ContextLineage lineage)
	{
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Correlation Chain Report: {correlationId}"));
		_ = sb.AppendLine(new string('=', 60));
		_ = sb.AppendLine();

		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Origin Message: {lineage.OriginMessageId}"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Start Time: {lineage.StartTime:yyyy-MM-dd HH:mm:ss.fff}"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Total Snapshots: {lineage.Snapshots.Count}"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Service Boundaries Crossed: {lineage.ServiceBoundaries.Count}"));
		_ = sb.AppendLine();
	}

	private static void AppendStageProgression(StringBuilder sb, ContextLineage lineage)
	{
		_ = sb.AppendLine("Stage Progression:");
		foreach (var snapshot in lineage.Snapshots)
		{
			_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" [{snapshot.Timestamp:HH:mm:ss.fff}] {snapshot.Stage}"));
			_ = sb.AppendLine(string.Create(
				CultureInfo.InvariantCulture,
				$" Fields: {snapshot.FieldCount}, Size: {snapshot.SizeBytes} bytes"));
		}

		_ = sb.AppendLine();
	}

	private static void AppendServiceBoundaries(StringBuilder sb, ContextLineage lineage)
	{
		if (lineage.ServiceBoundaries.Count != 0)
		{
			_ = sb.AppendLine("Service Boundaries:");
			foreach (var boundary in lineage.ServiceBoundaries)
			{
				var status = boundary.ContextPreserved ? "Preserved" : "Lost";
				_ = sb.AppendLine(string.Create(
					CultureInfo.InvariantCulture,
					$" [{boundary.Timestamp:HH:mm:ss.fff}] {boundary.ServiceName} - {status}"));
			}

			_ = sb.AppendLine();
		}
	}

	private static void AppendStatistics(StringBuilder sb, ContextLineage lineage)
	{
		var duration = lineage.Snapshots.Count != 0
			? lineage.Snapshots[^1].Timestamp - lineage.StartTime
			: TimeSpan.Zero;

		var avgFieldCount = lineage.Snapshots.Count != 0
			? lineage.Snapshots.Average(static s => s.FieldCount)
			: 0;

		var avgSize = lineage.Snapshots.Count != 0
			? lineage.Snapshots.Average(static s => s.SizeBytes)
			: 0;

		_ = sb.AppendLine("Statistics:");
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" Total Duration: {duration.TotalMilliseconds:F2}ms"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" Average Field Count: {avgFieldCount:F1}"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" Average Size: {avgSize:F0} bytes"));
		_ = sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $" Preservation Rate: {CalculatePreservationRate(lineage):P1}"));
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static List<string> CompareSnapshots(ContextSnapshot before, ContextSnapshot after)
	{
		var changes = new List<string>();

		// Check for added fields
		foreach (var field in after.Fields.Keys.Except(before.Fields.Keys, StringComparer.Ordinal))
		{
			changes.Add($"Added: {field}");
		}

		// Check for removed fields
		foreach (var field in before.Fields.Keys.Except(after.Fields.Keys, StringComparer.Ordinal))
		{
			changes.Add($"Removed: {field}");
		}

		// Check for modified fields
		foreach (var field in before.Fields.Keys.Intersect(after.Fields.Keys, StringComparer.Ordinal))
		{
			var beforeValue = JsonSerializer.Serialize(before.Fields[field]);
			var afterValue = JsonSerializer.Serialize(after.Fields[field]);

			if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
			{
				changes.Add($"Modified: {field}");
			}
		}

		return changes;
	}

	private static string FormatFieldValue(object value)
	{
		if (value == null)
		{
			return "null";
		}

		var str = value.ToString() ?? string.Empty;
		const int maxLength = 50;

		return str.Length > maxLength ? $"{str.AsSpan(0, maxLength)}..." : str;
	}

	private static string? GetContextFieldValue(IMessageContext context, string fieldName) =>
		fieldName switch
		{
			nameof(context.MessageId) => context.MessageId,
			nameof(context.CorrelationId) => context.CorrelationId,
			nameof(context.CausationId) => context.CausationId,
			nameof(context.TenantId) => context.TenantId,
			nameof(context.UserId) => context.UserId,
			nameof(context.MessageType) => context.MessageType,
			_ => null,
		};

	private static int CountContextFields(IMessageContext context)
	{
		var count = CountStandardFields(context);
		count += context.Items.Count;
		return count;
	}

	private static int CountStandardFields(IMessageContext context) =>
		CountNonNullFields(
			context.MessageId,
			context.ExternalId,
			context.UserId,
			context.CorrelationId,
			context.CausationId,
			context.TraceParent,
			context.SerializerVersion(),
			context.MessageVersion(),
			context.ContractVersion(),
			context.DesiredVersion(),
			context.TenantId,
			context.Source,
			context.MessageType,
			context.ContentType,
			context.PartitionKey(),
			context.ReplyTo(),
			context.SentTimestampUtc);

	private static int CountNonNullFields(params object?[] fields)
	{
		var count = 0;
		foreach (var field in fields)
		{
			if (field != null)
			{
				count++;
			}
		}

		return count;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static int EstimateContextSize(IMessageContext context)
	{
		try
		{
			var json = JsonSerializer.Serialize(new
			{
				context.MessageId,
				context.ExternalId,
				context.UserId,
				context.CorrelationId,
				context.CausationId,
				context.TraceParent,
				SerializerVersion = context.SerializerVersion(),
				MessageVersion = context.MessageVersion(),
				ContractVersion = context.ContractVersion(),
				DesiredVersion = context.DesiredVersion(),
				context.TenantId,
				context.Source,
				context.MessageType,
				context.ContentType,
				context.DeliveryCount,
				PartitionKey = context.PartitionKey(),
				ReplyTo = context.ReplyTo(),
				context.ReceivedTimestampUtc,
				context.SentTimestampUtc,
				context.Items,
			});

			return Encoding.UTF8.GetByteCount(json);
		}
		catch (NotSupportedException)
		{
			return 0;
		}
		catch (ArgumentException)
		{
			return 0;
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static int EstimateItemSize(object? value)
	{
		if (value == null)
		{
			return 0;
		}

		try
		{
			var json = JsonSerializer.Serialize(value);
			return Encoding.UTF8.GetByteCount(json);
		}
		catch (NotSupportedException)
		{
			return value.ToString()?.Length ?? 0;
		}
		catch (ArgumentException)
		{
			return value.ToString()?.Length ?? 0;
		}
	}

	private static bool ContainsPotentialPII(string text)
	{
		var upperText = text.ToUpperInvariant();
		return PiiPatterns.Any(upperText.Contains);
	}

	private void CheckMissingRequiredFields(IMessageContext context, List<ContextDiagnosticIssue> issues)
	{
		foreach (var field in _options.Fields.RequiredContextFields ?? [])
		{
			var value = GetContextFieldValue(context, field);
			if (string.IsNullOrWhiteSpace(value))
			{
				issues.Add(new ContextDiagnosticIssue
				{
					Severity = DiagnosticSeverity.Error,
					Category = "MissingField",
					Description = $"Required field '{field}' is missing or empty",
					Field = field,
					Recommendation = $"Ensure {field} is set before processing",
				});
			}
		}
	}

	[RequiresDynamicCode("Calls EstimateContextSize which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls EstimateContextSize which uses JSON serialization that cannot be statically analyzed")]
	private void CheckOversizedContext(IMessageContext context, List<ContextDiagnosticIssue> issues)
	{
		var contextSize = EstimateContextSize(context);
		if (contextSize > _options.Limits.MaxContextSizeBytes)
		{
			issues.Add(new ContextDiagnosticIssue
			{
				Severity = DiagnosticSeverity.Warning,
				Category = "OversizedContext",
				Description =
					$"Context size ({contextSize.ToString(CultureInfo.InvariantCulture)} bytes) exceeds threshold ({_options.Limits.MaxContextSizeBytes.ToString(CultureInfo.InvariantCulture)} bytes)",
				Field = null,
				Recommendation = "Consider reducing custom items or using external storage for large data",
			});
		}
	}

	private void RecordAnomaly(ContextAnomaly anomaly)
	{
		_anomalies.Enqueue(anomaly);
		var count = Interlocked.Increment(ref _anomalyCount);

		// Keep queue size limited — use volatile counter to avoid TOCTOU race on ConcurrentQueue.Count
		while (count > _options.Limits.MaxAnomalyQueueSize && _anomalies.TryDequeue(out _))
		{
			count = Interlocked.Decrement(ref _anomalyCount);
		}

		// Log based on severity
		var logLevel = anomaly.Severity switch
		{
			AnomalySeverity.High => LogLevel.Warning,
			AnomalySeverity.Medium => LogLevel.Information,
			_ => LogLevel.Debug,
		};

		LogContextAnomaly(logLevel, anomaly.Type.ToString(), anomaly.Description, anomaly.MessageId);
	}

	// Source-generated logging methods
	// Note: Dynamic LogLevel methods require named EventId parameter
	[LoggerMessage(EventId = ObservabilityEventId.HistoryEvent,
		Message = "Tracked history event {EventType} for message {MessageId}: {Details}")]
	private partial void LogHistoryEvent(LogLevel level, string eventType, string messageId, string details);

	[LoggerMessage(EventId = ObservabilityEventId.ContextAnomalyLogged,
		Message = "Context anomaly detected: {Type} - {Description} for message {MessageId}")]
	private partial void LogContextAnomaly(LogLevel level, string type, string description, string messageId);
}
