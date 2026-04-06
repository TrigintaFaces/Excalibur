// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Validates <see cref="OutboxDeliveryOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class OutboxDeliveryOptionsValidator : IValidateOptions<OutboxDeliveryOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OutboxDeliveryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PerRunTotal < 1)
		{
			failures.Add($"{nameof(OutboxDeliveryOptions.PerRunTotal)} must be >= 1 (was {options.PerRunTotal}).");
		}

		if (options.QueueCapacity < 1)
		{
			failures.Add($"{nameof(OutboxDeliveryOptions.QueueCapacity)} must be >= 1 (was {options.QueueCapacity}).");
		}

		if (options.ProducerBatchSize < 1)
		{
			failures.Add($"{nameof(OutboxDeliveryOptions.ProducerBatchSize)} must be >= 1 (was {options.ProducerBatchSize}).");
		}

		if (options.ConsumerBatchSize < 1)
		{
			failures.Add($"{nameof(OutboxDeliveryOptions.ConsumerBatchSize)} must be >= 1 (was {options.ConsumerBatchSize}).");
		}

		if (options.MaxAttempts < 1)
		{
			failures.Add($"{nameof(OutboxDeliveryOptions.MaxAttempts)} must be >= 1 (was {options.MaxAttempts}).");
		}

		if (options.BatchProcessing.ParallelProcessingDegree < 1)
		{
			failures.Add($"BatchProcessing.{nameof(OutboxBatchProcessingOptions.ParallelProcessingDegree)} must be >= 1 (was {options.BatchProcessing.ParallelProcessingDegree}).");
		}

		if (options.BatchProcessing.MinBatchSize < 1)
		{
			failures.Add($"BatchProcessing.{nameof(OutboxBatchProcessingOptions.MinBatchSize)} must be >= 1 (was {options.BatchProcessing.MinBatchSize}).");
		}

		if (options.BatchProcessing.MaxBatchSize < 1)
		{
			failures.Add($"BatchProcessing.{nameof(OutboxBatchProcessingOptions.MaxBatchSize)} must be >= 1 (was {options.BatchProcessing.MaxBatchSize}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
