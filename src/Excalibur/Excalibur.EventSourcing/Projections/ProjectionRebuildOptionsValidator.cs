// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Validates <see cref="ProjectionRebuildOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Every value here is a loop bound or a delay on a replay of the entire global stream, so a nonsensical
/// setting does not fail — it hangs, spins, or silently rebuilds nothing. A rebuild is also the operation a
/// consumer reaches for when a projection is already wrong, which is the worst moment to discover that a
/// batch size of zero makes no progress. Startup is where these are cheap to catch.
/// </remarks>
internal sealed class ProjectionRebuildOptionsValidator : IValidateOptions<ProjectionRebuildOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ProjectionRebuildOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// Zero would read no events per batch and never advance; negative is meaningless to a take count.
		if (options.BatchSize <= 0)
		{
			failures.Add(
				$"{nameof(ProjectionRebuildOptions.BatchSize)} must be greater than 0 (was {options.BatchSize}). "
				+ "A non-positive batch reads nothing and the rebuild never advances.");
		}

		// Negative is rejected; zero is legitimate and means "do not pace the replay".
		if (options.BatchDelay < TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(ProjectionRebuildOptions.BatchDelay)} must not be negative (was {options.BatchDelay}). "
				+ "Use TimeSpan.Zero to run without pacing.");
		}

		if (options.MaxParallelism <= 0)
		{
			failures.Add(
				$"{nameof(ProjectionRebuildOptions.MaxParallelism)} must be greater than 0 (was {options.MaxParallelism}). "
				+ "Use 1 to rebuild projections sequentially.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
