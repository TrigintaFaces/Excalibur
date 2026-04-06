// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Validates <see cref="InboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class InboxOptionsValidator : IValidateOptions<InboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, InboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxAttempts < 1)
		{
			failures.Add($"{nameof(InboxOptions.MaxAttempts)} must be >= 1 (was {options.MaxAttempts}).");
		}

		if (options.Capacity.PerRunTotal < 1)
		{
			failures.Add($"Capacity.{nameof(InboxCapacityOptions.PerRunTotal)} must be >= 1 (was {options.Capacity.PerRunTotal}).");
		}

		if (options.Capacity.QueueCapacity < 1)
		{
			failures.Add($"Capacity.{nameof(InboxCapacityOptions.QueueCapacity)} must be >= 1 (was {options.Capacity.QueueCapacity}).");
		}

		if (options.Capacity.ProducerBatchSize < 1)
		{
			failures.Add($"Capacity.{nameof(InboxCapacityOptions.ProducerBatchSize)} must be >= 1 (was {options.Capacity.ProducerBatchSize}).");
		}

		if (options.Capacity.ConsumerBatchSize < 1)
		{
			failures.Add($"Capacity.{nameof(InboxCapacityOptions.ConsumerBatchSize)} must be >= 1 (was {options.Capacity.ConsumerBatchSize}).");
		}

		if (options.Capacity.ParallelProcessingDegree < 1)
		{
			failures.Add($"Capacity.{nameof(InboxCapacityOptions.ParallelProcessingDegree)} must be >= 1 (was {options.Capacity.ParallelProcessingDegree}).");
		}

		if (options.BatchTuning.MinBatchSize < 1)
		{
			failures.Add($"BatchTuning.{nameof(InboxBatchTuningOptions.MinBatchSize)} must be >= 1 (was {options.BatchTuning.MinBatchSize}).");
		}

		if (options.BatchTuning.MaxBatchSize < 1)
		{
			failures.Add($"BatchTuning.{nameof(InboxBatchTuningOptions.MaxBatchSize)} must be >= 1 (was {options.BatchTuning.MaxBatchSize}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
