// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Validation;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Middleware that validates message context integrity to detect data loss or corruption during processing.
/// </summary>
/// <remarks>
/// This middleware runs early in the pipeline to catch context issues before they cause downstream problems. It validates required fields,
/// checks for corruption, and tracks metrics on validation failures.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="ContextValidationMiddleware" /> class. </remarks>
/// <param name="contextValidator"> The primary context validator. </param>
/// <param name="logger"> The logger for diagnostic output. </param>
/// <param name="options"> Configuration options for the middleware. </param>
/// <param name="customValidators"> Optional collection of custom validators. </param>
public sealed partial class ContextValidationMiddleware(
	IContextValidator contextValidator,
	ILogger<ContextValidationMiddleware> logger,
	IOptions<ContextValidationOptions> options,
	IEnumerable<IContextValidator>? customValidators = null) : IDispatchMiddleware
{
	private const string MetricsPrefix = "dispatch.context.validation";
	private static readonly Meter Meter = new("Excalibur.Dispatch.Messaging.ContextValidation", "1.0.0");

	/// <summary>
	/// Metrics instruments.
	/// </summary>
	private static readonly Counter<long> ValidationSuccessCounter = Meter.CreateCounter<long>(
		$"{MetricsPrefix}.success",
		description: "Count of successful context validations");

	private static readonly Counter<long> ValidationFailureCounter = Meter.CreateCounter<long>(
		$"{MetricsPrefix}.failure",
		description: "Count of failed context validations");

	private static readonly Counter<long> FieldsMissingCounter = Meter.CreateCounter<long>(
		$"{MetricsPrefix}.fields.missing",
		description: "Count of missing required fields");

	private static readonly Counter<long> FieldsCorruptedCounter = Meter.CreateCounter<long>(
		$"{MetricsPrefix}.fields.corrupted",
		description: "Count of corrupted fields detected");

	private static readonly Histogram<double> ValidationDurationHistogram = Meter.CreateHistogram<double>(
		$"{MetricsPrefix}.duration",
		unit: "ms",
		description: "Duration of context validation in milliseconds");

	private readonly IContextValidator _contextValidator = contextValidator ?? throw new ArgumentNullException(nameof(contextValidator));
	private readonly ILogger<ContextValidationMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ContextValidationOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly List<IContextValidator> _customValidators = customValidators?.ToList() ?? [];

	/// <summary>
	/// Gets the stage at which this middleware runs in the pipeline.
	/// </summary>
	/// <remarks> Runs at PreProcessing stage (100) to catch context issues early. </remarks>
	/// <value> The current <see cref="Stage" /> value. </value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <summary>
	/// Invokes the context validation middleware.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context to validate. </param>
	/// <param name="nextDelegate"> The next delegate in the pipeline. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> The result of message processing. </returns>
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			// Perform validation
			var validationResult = await ValidateContextAsync(message, context, cancellationToken).ConfigureAwait(false);

			// Record metrics
			RecordMetrics(validationResult, stopwatch.Elapsed.TotalMilliseconds);

			// Check if validation failed
			if (!validationResult.IsValid)
			{
				LogValidationFailure(validationResult, context);

				// In strict mode, reject the message
				if (_options.Mode == ValidationMode.Strict)
				{
					return CreateValidationFailureResult(validationResult, context);
				}

				// In lenient mode, log warning and continue
				LogContextValidationWarning(
					context.MessageId ?? "unknown",
					validationResult.FailureReason,
					validationResult.MissingFields.Count,
					validationResult.CorruptedFields.Count);
			}

			// Continue processing
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogContextValidationError(ex, context.MessageId ?? "unknown");

			// Record failure metric
			ValidationFailureCounter.Add(
				1,
				new KeyValuePair<string, object?>("reason", "exception"),
				new KeyValuePair<string, object?>("exception_type", ex.GetType().Name));

			// Rethrow if in strict mode
			if (_options.Mode == ValidationMode.Strict)
			{
				throw;
			}

			// Otherwise continue processing
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Merges two validation results into a single result.
	/// </summary>
	private static ContextValidationResult MergeResults(ContextValidationResult first, ContextValidationResult second)
	{
		if (first.IsValid && second.IsValid)
		{
			return first;
		}

		var missingFields = new HashSet<string>(first.MissingFields, StringComparer.Ordinal);
		missingFields.UnionWith(second.MissingFields);

		var corruptedFields = new HashSet<string>(first.CorruptedFields, StringComparer.Ordinal);
		corruptedFields.UnionWith(second.CorruptedFields);

		var details = new Dictionary<string, object?>(first.Details, StringComparer.Ordinal);
		foreach (var kvp in second.Details)
		{
			details[kvp.Key] = kvp.Value;
		}

		var failureReason = string.Join(
			"; ",
			new[] { first.FailureReason, second.FailureReason }
				.Where(static r => !string.IsNullOrWhiteSpace(r)));

		return new ContextValidationResult
		{
			IsValid = false,
			FailureReason = failureReason,
			MissingFields = [.. missingFields],
			CorruptedFields = [.. corruptedFields],
			Details = details,
		};
	}

	/// <summary>
	/// Records metrics for the validation operation.
	/// </summary>
	private static void RecordMetrics(ContextValidationResult result, double durationMs)
	{
		// Record duration
		ValidationDurationHistogram.Record(durationMs);

		if (result.IsValid)
		{
			ValidationSuccessCounter.Add(1);
		}
		else
		{
			ValidationFailureCounter.Add(
				1,
				new KeyValuePair<string, object?>("reason", result.FailureReason));

			// Record field-level metrics
			if (result.MissingFields.Count > 0)
			{
				FieldsMissingCounter.Add(
					result.MissingFields.Count,
					new KeyValuePair<string, object?>("fields", string.Join(',', result.MissingFields)));
			}

			if (result.CorruptedFields.Count > 0)
			{
				FieldsCorruptedCounter.Add(
					result.CorruptedFields.Count,
					new KeyValuePair<string, object?>("fields", string.Join(',', result.CorruptedFields)));
			}
		}
	}

	/// <summary>
	/// Creates a message result for validation failure.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification =
			"Validation result parameter reserved for future enrichment of error details with specific validation failure information")]
	private static Excalibur.Dispatch.Messaging.MessageResult CreateValidationFailureResult(
		ContextValidationResult validationResult,
		IMessageContext context)
	{
		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Validation,
			Title = "Context Validation Failed",
			Status = 400,
			Detail = "An error occurred",
			Instance = context.CorrelationId ?? Uuid7Extensions.GenerateGuid().ToString(),
		};
		return new Excalibur.Dispatch.Messaging.MessageResult(succeeded: false, problemDetails: problemDetails);
	}

	/// <summary>
	/// Validates the message context using all configured validators.
	/// </summary>
	private async Task<ContextValidationResult> ValidateContextAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Start with the primary validator
		var result = await _contextValidator.ValidateAsync(message, context, cancellationToken).ConfigureAwait(false);

		// If primary validation failed and we're in strict mode, return immediately
		if (!result.IsValid && _options.Mode == ValidationMode.Strict)
		{
			return result;
		}

		// Apply custom validators
		foreach (var validator in _customValidators)
		{
			var customResult = await validator.ValidateAsync(message, context, cancellationToken).ConfigureAwait(false);

			// Merge results
			result = MergeResults(result, customResult);

			// Stop on first failure in strict mode
			if (!result.IsValid && _options.Mode == ValidationMode.Strict)
			{
				break;
			}
		}

		return result;
	}

	/// <summary>
	/// Logs detailed validation failure information.
	/// </summary>
	private void LogValidationFailure(ContextValidationResult result, IMessageContext context)
	{
		LogContextValidationFailed(
			context.MessageId ?? "unknown",
			context.MessageType ?? "unknown",
			result.FailureReason,
			string.Join(", ", result.MissingFields),
			string.Join(", ", result.CorruptedFields));

		// Log additional details if debug logging is enabled
		if (result.Details.Count > 0)
		{
			foreach (var detail in result.Details)
			{
				LogContextValidationDetail(detail.Key, detail.Value?.ToString() ?? "null");
			}
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.ContextValidationFailed, LogLevel.Error,
		"Context validation failed for message {MessageId} of type {MessageType}: {Reason}. Missing fields: {MissingFields}. Corrupted fields: {CorruptedFields}")]
	private partial void LogContextValidationFailed(string messageId, string messageType, string reason, string missingFields,
		string corruptedFields);

	[LoggerMessage(MiddlewareEventId.ContextValidationWarning, LogLevel.Warning,
		"Context validation warning for message {MessageId}: {Reason}. Missing: {MissingCount} fields, Corrupted: {CorruptedCount} fields")]
	private partial void LogContextValidationWarning(string messageId, string reason, int missingCount, int corruptedCount);

	[LoggerMessage(MiddlewareEventId.ContextValidationDetail, LogLevel.Debug,
		"Context validation detail - {Key}: {Value}")]
	private partial void LogContextValidationDetail(string key, string value);

	[LoggerMessage(MiddlewareEventId.ContextValidationError, LogLevel.Error,
		"Context validation error for message {MessageId}")]
	private partial void LogContextValidationError(Exception ex, string messageId);
}
