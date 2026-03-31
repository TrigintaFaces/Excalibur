// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Validates <see cref="ParallelCatchUpOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class ParallelCatchUpOptionsValidator : IValidateOptions<ParallelCatchUpOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ParallelCatchUpOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.WorkerCount <= 0)
		{
			failures.Add($"{nameof(ParallelCatchUpOptions.WorkerCount)} must be greater than 0 (was {options.WorkerCount}).");
		}

		if (options.BatchSize <= 0)
		{
			failures.Add($"{nameof(ParallelCatchUpOptions.BatchSize)} must be greater than 0 (was {options.BatchSize}).");
		}

		if (options.CheckpointInterval <= 0)
		{
			failures.Add($"{nameof(ParallelCatchUpOptions.CheckpointInterval)} must be greater than 0 (was {options.CheckpointInterval}).");
		}

		if (options.MaxRetries < 0)
		{
			failures.Add($"{nameof(ParallelCatchUpOptions.MaxRetries)} must be at least 0 (was {options.MaxRetries}).");
		}

		if (options.WorkerHeartbeatTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(ParallelCatchUpOptions.WorkerHeartbeatTimeout)} must be greater than TimeSpan.Zero (was {options.WorkerHeartbeatTimeout}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
