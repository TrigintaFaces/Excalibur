// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Validates <see cref="BatchOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks beyond what DataAnnotations can express
/// (e.g., <c>MinMessagesPerBatch &lt;= MaxMessagesPerBatch</c>, TimeSpan positivity).
/// </remarks>
internal sealed class BatchOptionsValidator : IValidateOptions<BatchOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, BatchOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxMessagesPerBatch is <= 0 or > 1000)
		{
			failures.Add($"{nameof(BatchOptions.MaxMessagesPerBatch)} must be between 1 and 1000 (Pub/Sub API limit). Was {options.MaxMessagesPerBatch}.");
		}

		if (options.MinMessagesPerBatch < 1 || options.MinMessagesPerBatch > options.MaxMessagesPerBatch)
		{
			failures.Add($"{nameof(BatchOptions.MinMessagesPerBatch)} must be between 1 and {nameof(BatchOptions.MaxMessagesPerBatch)} ({options.MaxMessagesPerBatch}). Was {options.MinMessagesPerBatch}.");
		}

		if (options.MaxBatchWaitTime <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(BatchOptions.MaxBatchWaitTime)} must be greater than zero. Was {options.MaxBatchWaitTime}.");
		}

		if (options.MaxBatchSizeBytes <= 0)
		{
			failures.Add($"{nameof(BatchOptions.MaxBatchSizeBytes)} must be greater than zero. Was {options.MaxBatchSizeBytes}.");
		}

		if (options.TargetBatchProcessingTime <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(BatchOptions.TargetBatchProcessingTime)} must be greater than zero. Was {options.TargetBatchProcessingTime}.");
		}

		if (options.ConcurrentBatchProcessors <= 0)
		{
			failures.Add($"{nameof(BatchOptions.ConcurrentBatchProcessors)} must be greater than zero. Was {options.ConcurrentBatchProcessors}.");
		}

		if (options.Acknowledgment.AckDeadlineSeconds is < 10 or > 600)
		{
			failures.Add($"{nameof(BatchAcknowledgmentOptions)}.{nameof(BatchAcknowledgmentOptions.AckDeadlineSeconds)} must be between 10 and 600. Was {options.Acknowledgment.AckDeadlineSeconds}.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
