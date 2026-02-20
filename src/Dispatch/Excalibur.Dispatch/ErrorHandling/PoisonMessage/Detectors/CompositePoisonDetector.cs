// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Composite detector that aggregates multiple poison message detectors.
/// </summary>
public sealed partial class CompositePoisonDetector : IPoisonMessageDetector
{
	private readonly IEnumerable<IPoisonMessageDetector> _detectors;
	private readonly ILogger<CompositePoisonDetector> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompositePoisonDetector" /> class.
	/// </summary>
	/// <param name="detectors"> The collection of poison message detectors. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	public CompositePoisonDetector(
		IEnumerable<IPoisonMessageDetector> detectors,
		ILogger<CompositePoisonDetector> logger)
	{
		ArgumentNullException.ThrowIfNull(detectors);
		ArgumentNullException.ThrowIfNull(logger);

		// Filter out self to avoid circular references
		_detectors = detectors.Where(static d => d.GetType() != typeof(CompositePoisonDetector)).ToList();
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<PoisonDetectionResult> IsPoisonMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		MessageProcessingInfo processingInfo,
		Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(processingInfo);

		foreach (var detector in _detectors)
		{
			try
			{
				var result = await detector.IsPoisonMessageAsync(message, context, processingInfo, exception)
					.ConfigureAwait(false);

				if (result.IsPoison)
				{
					var messageId = context.MessageId ?? "Unknown";
					var detectorName = result.DetectorName ?? "Unknown";
					var reason = result.Reason ?? "No reason provided";
					LogMessageDetectedAsPoisonByDetector(messageId, detectorName, reason);

					return result;
				}
			}
			catch (Exception ex)
			{
				var messageId = context.MessageId ?? "Unknown";
				LogErrorInPoisonDetectorForMessage(detector.GetType().Name, messageId, ex);
			}
		}

		return PoisonDetectionResult.NotPoison();
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.PoisonMessageDetected, LogLevel.Information,
		"Message '{MessageId}' detected as poison by detector '{DetectorName}': {Reason}")]
	private partial void LogMessageDetectedAsPoisonByDetector(string messageId, string detectorName, string reason);

	[LoggerMessage(DeliveryEventId.PoisonDetectorError, LogLevel.Error,
		"Error in poison detector '{DetectorName}' for message '{MessageId}'")]
	private partial void LogErrorInPoisonDetectorForMessage(string detectorName, string messageId, Exception ex);
}
