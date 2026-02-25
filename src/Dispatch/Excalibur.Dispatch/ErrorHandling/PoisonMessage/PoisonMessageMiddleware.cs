// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Middleware that detects and handles poison messages during dispatch processing.
/// </summary>
public sealed partial class PoisonMessageMiddleware : IDispatchMiddleware, IDisposable
{
	private readonly IPoisonMessageDetector _poisonDetector;
	private readonly IPoisonMessageHandler _poisonHandler;
	private readonly PoisonMessageOptions _options;
	private readonly ILogger<PoisonMessageMiddleware> _logger;
	private readonly ActivitySource _activitySource;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PoisonMessageMiddleware" /> class.
	/// </summary>
	public PoisonMessageMiddleware(
		IPoisonMessageDetector poisonDetector,
		IPoisonMessageHandler poisonHandler,
		IOptions<PoisonMessageOptions> options,
		ILogger<PoisonMessageMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(poisonDetector);
		ArgumentNullException.ThrowIfNull(poisonHandler);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_poisonDetector = poisonDetector;
		_poisonHandler = poisonHandler;
		_options = options.Value;
		_logger = logger;
		_activitySource = new ActivitySource(DispatchTelemetryConstants.ActivitySources.PoisonMessageMiddleware);
	}

	/// <summary>
	/// Gets the stage at which this middleware should execute in the pipeline.
	/// </summary>
	/// <value>The current <see cref="Stage"/> value.</value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"AotAnalysis",
			"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
			Justification = "Poison message handling relies on dynamic serialization and is not supported for AOT scenarios.")]
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
			Justification = "Poison message handling relies on serialization of known message types registered at startup.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		using var activity = _activitySource.StartActivity("PoisonMessageDetection");
		_ = (activity?.SetTag("message.id", context.MessageId));
		_ = (activity?.SetTag("message.type", message.GetType().Name));

		// Track processing attempt
		IncrementProcessingAttempt(context);
		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			// Execute the rest of the pipeline
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			// Record successful processing
			RecordProcessingAttempt(context, stopwatch.Elapsed, succeeded: true, exception: null);

			return result;
		}
		catch (Exception ex)
		{
			// Record failed processing
			RecordProcessingAttempt(context, stopwatch.Elapsed, succeeded: false, ex);

			// Extract processing info
			var processingInfo = BuildProcessingInfo(context);

			// Check if this is a poison message
			var detectionResult = await _poisonDetector.IsPoisonMessageAsync(
				message,
				context,
				processingInfo,
				ex).ConfigureAwait(false);

			if (detectionResult.IsPoison)
			{
				LogPoisonMessageDetected(
					context.MessageId ?? "null",
					detectionResult.Reason ?? "Unknown",
					detectionResult.DetectorName ?? "Unknown");

				_ = (activity?.SetTag("poison.detected", value: true));
				_ = (activity?.SetTag("poison.reason", detectionResult.Reason));
				_ = (activity?.SetTag("poison.detector", detectionResult.DetectorName));

				try
				{
					// Handle the poison message
					await _poisonHandler.HandlePoisonMessageAsync(
						message,
						context,
						detectionResult.Reason ?? "Unknown reason",
						cancellationToken,
						ex).ConfigureAwait(false);

					// Return a failed result indicating poison message
					var problemDetails = new MessageProblemDetails
					{
						Type = "PoisonMessage",
						Title = "Poison Message Detected",
						ErrorCode = 503,
						Status = 503,
						Detail = detectionResult.Reason ?? "Unknown reason",
						Instance = context.MessageId ?? "Unknown",
					};

					var result = new Excalibur.Dispatch.Messaging.MessageResult(
						succeeded: false,
						problemDetails: problemDetails);

					_ = (result.ProblemDetails?.Extensions?.TryAdd("poisonDetector", detectionResult.DetectorName));
					_ = (result.ProblemDetails?.Extensions?.TryAdd("processingAttempts", processingInfo.AttemptCount));
					_ = (result.ProblemDetails?.Extensions?.TryAdd("movedToDeadLetter", value: true));

					return result;
				}
				catch (Exception handlerEx)
				{
					LogPoisonHandlerFailed(context.MessageId ?? "null", handlerEx);

					// Re-throw the original exception if we can't handle the poison message
					throw;
				}
			}

			// Not a poison message, re-throw to let error handling middleware deal with it
			throw;
		}
	}

	/// <summary>
	/// Disposes the middleware and releases the activity source.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_activitySource?.Dispose();
		_disposed = true;
	}

	/// <summary>
	/// Increments the processing attempt counter in the context.
	/// </summary>
	private static void IncrementProcessingAttempt(IMessageContext context)
	{
		var attemptCount = 1;
		if (context.Items.TryGetValue("ProcessingAttempts", out var attempts) && attempts is int count)
		{
			attemptCount = count + 1;
		}

		context.Items["ProcessingAttempts"] = attemptCount;

		// Set first attempt time if not already set
		if (!context.Items.ContainsKey("FirstAttemptTime"))
		{
			context.Items["FirstAttemptTime"] = DateTimeOffset.UtcNow;
		}

		context.Items["CurrentAttemptTime"] = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Records a processing attempt in the context.
	/// </summary>
	private static void RecordProcessingAttempt(
		IMessageContext context,
		TimeSpan duration,
		bool succeeded,
		Exception? exception)
	{
		var history = new List<ProcessingAttempt>();
		if (context.Items.TryGetValue("ProcessingHistory", out var existingHistory) &&
			existingHistory is List<ProcessingAttempt> existing)
		{
			history = existing;
		}
		else
		{
			context.Items["ProcessingHistory"] = history;
		}

		var attemptNumber = 1;
		if (context.Items.TryGetValue("ProcessingAttempts", out var attempts) && attempts is int count)
		{
			attemptNumber = count;
		}

		history.Add(new ProcessingAttempt
		{
			AttemptNumber = attemptNumber,
			AttemptTime = DateTimeOffset.UtcNow,
			Duration = duration,
			Succeeded = succeeded,
			ErrorMessage = exception?.Message,
			ExceptionType = exception?.GetType().FullName,
		});
	}

	/// <summary>
	/// Builds processing info from the context.
	/// </summary>
	private static MessageProcessingInfo BuildProcessingInfo(IMessageContext context)
	{
		var info = new MessageProcessingInfo
		{
			AttemptCount = 1,
			FirstAttemptTime = DateTimeOffset.UtcNow,
			CurrentAttemptTime = DateTimeOffset.UtcNow,
		};

		if (context.Items.TryGetValue("ProcessingAttempts", out var attempts) && attempts is int count)
		{
			info.AttemptCount = count;
		}

		if (context.Items.TryGetValue("FirstAttemptTime", out var firstTime) && firstTime is DateTimeOffset first)
		{
			info.FirstAttemptTime = first;
		}

		if (context.Items.TryGetValue("CurrentAttemptTime", out var currentTime) && currentTime is DateTimeOffset current)
		{
			info.CurrentAttemptTime = current;
		}

		if (context.Items.TryGetValue("ProcessingHistory", out var history) &&
			history is List<ProcessingAttempt> processingHistory)
		{
			foreach (var attempt in processingHistory)
			{
				info.ProcessingHistory.Add(attempt);
			}
		}

		info.TotalProcessingTime = info.CurrentAttemptTime - info.FirstAttemptTime;

		return info;
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.PoisonMiddlewareDetected, LogLevel.Warning,
		"Poison message detected. MessageId: {MessageId}, Reason: {Reason}, Detector: {Detector}")]
	private partial void LogPoisonMessageDetected(string messageId, string reason, string detector);

	[LoggerMessage(DeliveryEventId.PoisonMiddlewareHandlerError, LogLevel.Error,
		"Failed to handle poison message {MessageId}")]
	private partial void LogPoisonHandlerFailed(string messageId, Exception ex);
}
