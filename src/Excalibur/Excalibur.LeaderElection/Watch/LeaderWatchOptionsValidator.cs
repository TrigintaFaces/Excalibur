// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Watch;

/// <summary>
/// Validates <see cref="LeaderWatchOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces <c>[Range(typeof(TimeSpan), ...)]</c> attribute with an AOT-safe check.
/// </summary>
internal sealed class LeaderWatchOptionsValidator : IValidateOptions<LeaderWatchOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, LeaderWatchOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.PollInterval < TimeSpan.FromSeconds(1) || options.PollInterval > TimeSpan.FromHours(1))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(LeaderWatchOptions.PollInterval)} must be between 00:00:01 and 01:00:00 (was {options.PollInterval}).");
		}

		return ValidateOptionsResult.Success;
	}
}
