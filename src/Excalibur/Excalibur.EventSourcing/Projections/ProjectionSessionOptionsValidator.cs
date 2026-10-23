// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Validates <see cref="ProjectionSessionOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class ProjectionSessionOptionsValidator : IValidateOptions<ProjectionSessionOptions>
{
	private static readonly TimeSpan MinBound = TimeSpan.FromMilliseconds(50);
	private static readonly TimeSpan MaxBound = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan MinPoll = TimeSpan.FromMilliseconds(1);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ProjectionSessionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.Bound < MinBound || options.Bound > MaxBound)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(ProjectionSessionOptions)}.{nameof(ProjectionSessionOptions.Bound)} must be "
				+ $"between {MinBound} and {MaxBound} (was {options.Bound}).");
		}

		if (options.PollInterval < MinPoll)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(ProjectionSessionOptions)}.{nameof(ProjectionSessionOptions.PollInterval)} must "
				+ $"be at least {MinPoll} (was {options.PollInterval}).");
		}

		// A poll interval longer than the bound means the read waits the whole bound and looks exactly
		// once, at the start -- so it reports BoundExpired for a projection that caught up immediately
		// afterwards. That is a configuration that cannot do its job, caught here rather than at runtime
		// where it would look like a slow projection.
		if (options.PollInterval > options.Bound)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(ProjectionSessionOptions)}.{nameof(ProjectionSessionOptions.PollInterval)} "
				+ $"({options.PollInterval}) must not exceed "
				+ $"{nameof(ProjectionSessionOptions.Bound)} ({options.Bound}), or a consistent read would "
				+ "check the projection's progress only once and report it behind whatever it did next.");
		}

		return ValidateOptionsResult.Success;
	}
}
