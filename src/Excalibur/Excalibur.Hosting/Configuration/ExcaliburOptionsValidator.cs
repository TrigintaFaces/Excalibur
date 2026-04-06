// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Validates <see cref="ExcaliburOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class ExcaliburOptionsValidator : IValidateOptions<ExcaliburOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ExcaliburOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("ExcaliburOptions cannot be null.");
		}

		var failures = new List<string>();

		// EventSourcing nested validation
		if (options.EventSourcing is not null)
		{
			if (options.EventSourcing.SnapshotFrequency < 1)
			{
				failures.Add("ExcaliburOptions.EventSourcing.SnapshotFrequency must be at least 1.");
			}

			if (options.EventSourcing.DefaultReadBatchSize < 1)
			{
				failures.Add("ExcaliburOptions.EventSourcing.DefaultReadBatchSize must be at least 1.");
			}
		}

		// Outbox nested validation
		if (options.Outbox is not null)
		{
			if (options.Outbox.MaxBatchSize < 1)
			{
				failures.Add("ExcaliburOptions.Outbox.MaxBatchSize must be at least 1.");
			}

			if (options.Outbox.MaxRetryAttempts < 1)
			{
				failures.Add("ExcaliburOptions.Outbox.MaxRetryAttempts must be at least 1.");
			}
		}

		// CDC nested validation
		if (options.Cdc is not null)
		{
			if (options.Cdc.MaxBatchSize < 1)
			{
				failures.Add("ExcaliburOptions.Cdc.MaxBatchSize must be at least 1.");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
