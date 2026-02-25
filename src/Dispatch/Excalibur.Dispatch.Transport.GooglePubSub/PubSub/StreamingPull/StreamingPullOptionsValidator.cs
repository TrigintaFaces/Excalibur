// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Validates <see cref="StreamingPullOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs both <see cref="System.ComponentModel.DataAnnotations"/> validation and
/// cross-property constraint checks (e.g., <c>StreamIdleTimeout > StreamAckDeadlineSeconds</c>).
/// </remarks>
public sealed class StreamingPullOptionsValidator : IValidateOptions<StreamingPullOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, StreamingPullOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.ConcurrentStreams is < 1 or > 32)
		{
			failures.Add($"{nameof(StreamingPullOptions.ConcurrentStreams)} must be between 1 and 32 (was {options.ConcurrentStreams}).");
		}

		if (options.MaxOutstandingMessagesPerStream is < 10 or > 10000)
		{
			failures.Add($"{nameof(StreamingPullOptions.MaxOutstandingMessagesPerStream)} must be between 10 and 10000 (was {options.MaxOutstandingMessagesPerStream}).");
		}

		if (options.MaxOutstandingBytesPerStream is < 1048576 or > 1073741824)
		{
			failures.Add($"{nameof(StreamingPullOptions.MaxOutstandingBytesPerStream)} must be between 1MB and 1GB (was {options.MaxOutstandingBytesPerStream}).");
		}

		if (options.StreamAckDeadlineSeconds is < 10 or > 600)
		{
			failures.Add($"{nameof(StreamingPullOptions.StreamAckDeadlineSeconds)} must be between 10 and 600 (was {options.StreamAckDeadlineSeconds}).");
		}

		if (options.AckExtensionThresholdPercent is < 50 or > 95)
		{
			failures.Add($"{nameof(StreamingPullOptions.AckExtensionThresholdPercent)} must be between 50 and 95 (was {options.AckExtensionThresholdPercent}).");
		}

		// Cross-property constraints
		if (options.StreamIdleTimeout <= TimeSpan.FromSeconds(options.StreamAckDeadlineSeconds))
		{
			failures.Add(
				$"{nameof(StreamingPullOptions.StreamIdleTimeout)} ({options.StreamIdleTimeout}) must be greater than " +
				$"{nameof(StreamingPullOptions.StreamAckDeadlineSeconds)} ({options.StreamAckDeadlineSeconds} seconds).");
		}

		if (options.HealthCheckInterval >= options.StreamIdleTimeout)
		{
			failures.Add(
				$"{nameof(StreamingPullOptions.HealthCheckInterval)} ({options.HealthCheckInterval}) must be less than " +
				$"{nameof(StreamingPullOptions.StreamIdleTimeout)} ({options.StreamIdleTimeout}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
