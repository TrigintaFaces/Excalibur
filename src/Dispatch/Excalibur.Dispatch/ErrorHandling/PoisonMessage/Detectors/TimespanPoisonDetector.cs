// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Detects poison messages based on processing timeout.
/// </summary>
public sealed class TimespanPoisonDetector : IPoisonMessageDetector
{
	private readonly PoisonMessageOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="TimespanPoisonDetector" /> class.
	/// </summary>
	/// <param name="options"> The poison message configuration options. </param>
	public TimespanPoisonDetector(IOptions<PoisonMessageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	public Task<PoisonDetectionResult> IsPoisonMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		MessageProcessingInfo processingInfo,
		Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(processingInfo);

		var totalTime = processingInfo.CurrentAttemptTime - processingInfo.FirstAttemptTime;

		if (totalTime >= _options.MaxProcessingTime)
		{
			var details = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["TotalProcessingTime"] = totalTime.ToString(),
				["MaxProcessingTime"] = _options.MaxProcessingTime.ToString(),
				["FirstAttemptTime"] = processingInfo.FirstAttemptTime,
				["CurrentAttemptTime"] = processingInfo.CurrentAttemptTime,
				["AttemptCount"] = processingInfo.AttemptCount,
			};

			// Add processing history summary
			if (processingInfo.ProcessingHistory.Count > 0)
			{
				details["AverageAttemptDuration"] = TimeSpan.FromMilliseconds(
					processingInfo.ProcessingHistory.Average(static h => h.Duration.TotalMilliseconds)).ToString();
				details["FailedAttempts"] = processingInfo.ProcessingHistory.Count(static h => !h.Succeeded);
			}

			return Task.FromResult(
				PoisonDetectionResult.Poison(
					$"Message exceeded maximum processing time ({totalTime:hh\\:mm\\:ss}/{_options.MaxProcessingTime:hh\\:mm\\:ss})",
					nameof(TimespanPoisonDetector),
					details));
		}

		return Task.FromResult(PoisonDetectionResult.NotPoison());
	}
}
