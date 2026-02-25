// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Custom validator that specifically validates distributed tracing context integrity.
/// </summary>
/// <remarks>
/// This validator ensures that distributed tracing context is properly maintained throughout the message processing pipeline, detecting
/// breaks in the trace chain.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="TraceContextValidator" /> class. </remarks>
/// <param name="logger"> The logger for diagnostic output. </param>
public sealed partial class TraceContextValidator(ILogger<TraceContextValidator> logger) : IContextValidator
{
	private readonly ILogger<TraceContextValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public ValueTask<ContextValidationResult> ValidateAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var details = new Dictionary<string, object?>(StringComparer.Ordinal);
		var issues = new List<string>();

		// Get current activity
		var currentActivity = Activity.Current;

		// Check if we have an activity when we should
		if (currentActivity == null && !string.IsNullOrWhiteSpace(context.TraceParent))
		{
			issues.Add("Activity.Current is null but context has TraceParent");
			details["Expected_TraceParent"] = context.TraceParent;
			details["Activity_Missing"] = true;
		}

		// Validate trace continuity
		if (currentActivity != null)
		{
			// Check if trace IDs match
			var activityTraceId = currentActivity.TraceId.ToString();

			if (!string.IsNullOrWhiteSpace(context.TraceParent))
			{
				// Extract trace ID from W3C format
				var contextTraceId = ExtractTraceIdFromTraceParent(context.TraceParent);

				if (!string.IsNullOrWhiteSpace(contextTraceId) && !string.Equals(activityTraceId, contextTraceId, StringComparison.Ordinal))
				{
					issues.Add("Trace ID mismatch between Activity and context");
					details["Activity_TraceId"] = activityTraceId;
					details["Context_TraceId"] = contextTraceId;
				}
			}

			// Check for baggage continuity
			ValidateBaggage(currentActivity, context, issues, details);

			// Check for span hierarchy
			ValidateSpanHierarchy(currentActivity, context, issues, details);

			// Validate activity tags match context
			ValidateActivityTags(currentActivity, context, issues, details);
		}

		// Check for orphaned trace contexts
		if (!string.IsNullOrWhiteSpace(context.TraceParent) && string.IsNullOrWhiteSpace(context.MessageId))
		{
			issues.Add("TraceParent present but MessageId is missing");
			details["Orphaned_Trace"] = true;
		}

		// Build result
		if (issues.Count == 0)
		{
			return ValueTask.FromResult(ContextValidationResult.Success());
		}

		LogTraceValidationIssues(string.Join("; ", issues), details.Count);

		return ValueTask.FromResult(new ContextValidationResult
		{
			IsValid = false,
			FailureReason = $"Trace context validation failed: {string.Join("; ", issues)}",
			CorruptedFields = ["TraceContext"],
			Details = details,
			Severity = ValidationSeverity.Warning,
		});
	}

	/// <summary>
	/// Extracts the trace ID from a W3C TraceParent string.
	/// </summary>
	private static string? ExtractTraceIdFromTraceParent(string traceParent)
	{
		// W3C format: version-trace-id-parent-id-trace-flags
		var parts = traceParent.Split('-');
		return parts.Length >= 2 ? parts[1] : null;
	}

	/// <summary>
	/// Validates baggage continuity between activity and context.
	/// </summary>
	private static void ValidateBaggage(
		Activity activity,
		IMessageContext context,
		List<string> issues,
		Dictionary<string, object?> details)
	{
		var activityBaggage = activity.Baggage.ToList();

		// Check for expected baggage items
		var expectedBaggage = new[] { "correlation-id", "tenant-id", "user-id" };

		foreach (var expected in expectedBaggage)
		{
			var baggageValue = activityBaggage.FirstOrDefault(b => string.Equals(b.Key, expected, StringComparison.Ordinal)).Value;

			switch (expected)
			{
				case "correlation-id" when context.CorrelationId != null:
					if (!string.Equals(baggageValue, context.CorrelationId, StringComparison.Ordinal))
					{
						issues.Add($"Baggage mismatch for {expected}");
						details[$"Baggage_{expected}"] = baggageValue;
						details[$"Context_{expected}"] = context.CorrelationId;
					}

					break;

				case "tenant-id" when context.TenantId != null:
					if (!string.Equals(baggageValue, context.TenantId, StringComparison.Ordinal))
					{
						issues.Add($"Baggage mismatch for {expected}");
						details[$"Baggage_{expected}"] = baggageValue;
						details[$"Context_{expected}"] = context.TenantId;
					}

					break;

				case "user-id" when !string.IsNullOrWhiteSpace(context.UserId):
					if (!string.Equals(baggageValue, context.UserId, StringComparison.Ordinal))
					{
						issues.Add($"Baggage mismatch for {expected}");
						details[$"Baggage_{expected}"] = baggageValue;
						details[$"Context_{expected}"] = context.UserId;
					}

					break;
				default:
					break;
			}
		}
	}

	/// <summary>
	/// Validates span hierarchy is maintained.
	/// </summary>
	private static void ValidateSpanHierarchy(
		Activity activity,
		IMessageContext context,
		List<string> issues,
		Dictionary<string, object?> details)
	{
		// Check if the activity has a proper parent
		if (activity.Parent == null && activity.ParentId == null)
		{
			// Root span - should have no causation ID
			if (context.CausationId != null)
			{
				issues.Add("Root activity but context has CausationId");
				details["Unexpected_CausationId"] = context.CausationId;
			}
		}
		else
		{
			// Child span - should have causation ID
			if (context.CausationId == null)
			{
				issues.Add("Child activity but context missing CausationId");
				details["Activity_ParentId"] = activity.ParentId ?? activity.Parent?.Id;
			}
		}

		// Validate activity kind matches message type
		if (activity.Kind == ActivityKind.Producer && string.IsNullOrWhiteSpace(context.MessageType))
		{
			issues.Add("Producer activity but no MessageType in context");
			details["Activity_Kind"] = activity.Kind.ToString();
		}
	}

	/// <summary>
	/// Validates activity tags match context values.
	/// </summary>
	private static void ValidateActivityTags(
		Activity activity,
		IMessageContext context,
		List<string> issues,
		Dictionary<string, object?> details)
	{
		// Check standard tags
		var messageIdTag = activity.GetTagItem("messaging.message_id")?.ToString();
		if (messageIdTag != null && !string.Equals(messageIdTag, context.MessageId, StringComparison.Ordinal))
		{
			issues.Add("Activity tag 'messaging.message_id' doesn't match context");
			details["Tag_MessageId"] = messageIdTag;
			details["Context_MessageId"] = context.MessageId;
		}

		var messageTypeTag = activity.GetTagItem("messaging.message_type")?.ToString();
		if (messageTypeTag != null && !string.Equals(messageTypeTag, context.MessageType, StringComparison.Ordinal))
		{
			issues.Add("Activity tag 'messaging.message_type' doesn't match context");
			details["Tag_MessageType"] = messageTypeTag;
			details["Context_MessageType"] = context.MessageType;
		}

		// Check custom tags
		var correlationTag = activity.GetTagItem("messaging.correlation_id")?.ToString();
		if (correlationTag != null && context.CorrelationId != null &&
!string.Equals(correlationTag, context.CorrelationId, StringComparison.Ordinal))
		{
			issues.Add("Activity tag 'messaging.correlation_id' doesn't match context");
			details["Tag_CorrelationId"] = correlationTag;
			details["Context_CorrelationId"] = context.CorrelationId;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.TraceValidationIssues, LogLevel.Warning,
		"Trace context validation found issues: {Issues}. Details count: {DetailsCount}")]
	private partial void LogTraceValidationIssues(string issues, int detailsCount);
}
