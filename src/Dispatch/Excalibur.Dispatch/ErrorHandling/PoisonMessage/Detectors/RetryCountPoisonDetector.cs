// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Detects poison messages based on the number of retry attempts.
/// </summary>
public sealed class RetryCountPoisonDetector : IPoisonMessageDetector
{
	private readonly PoisonMessageOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RetryCountPoisonDetector" /> class.
	/// </summary>
	/// <param name="options"> The poison message configuration options. </param>
	public RetryCountPoisonDetector(IOptions<PoisonMessageOptions> options)
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

		if (processingInfo.AttemptCount >= _options.MaxRetryAttempts)
		{
			var details = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["AttemptCount"] = processingInfo.AttemptCount,
				["MaxRetryAttempts"] = _options.MaxRetryAttempts,
				["FirstAttemptTime"] = processingInfo.FirstAttemptTime,
				["CurrentAttemptTime"] = processingInfo.CurrentAttemptTime,
			};

			return Task.FromResult(
				PoisonDetectionResult.Poison(
					$"Message exceeded maximum retry attempts ({processingInfo.AttemptCount}/{_options.MaxRetryAttempts})",
					nameof(RetryCountPoisonDetector),
					details));
		}

		return Task.FromResult(PoisonDetectionResult.NotPoison());
	}
}
