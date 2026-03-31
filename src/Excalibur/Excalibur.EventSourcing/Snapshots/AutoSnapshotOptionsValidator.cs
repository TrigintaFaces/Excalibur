// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Validates <see cref="AutoSnapshotOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AutoSnapshotOptionsValidator : IValidateOptions<AutoSnapshotOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AutoSnapshotOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.EventCountThreshold is { } eventCount && eventCount <= 0)
		{
			failures.Add($"{nameof(AutoSnapshotOptions.EventCountThreshold)} must be greater than 0 when set (was {eventCount}).");
		}

		if (options.TimeThreshold is { } timeThreshold && timeThreshold <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(AutoSnapshotOptions.TimeThreshold)} must be greater than TimeSpan.Zero when set (was {timeThreshold}).");
		}

		if (options.VersionThreshold is { } versionThreshold && versionThreshold <= 0)
		{
			failures.Add($"{nameof(AutoSnapshotOptions.VersionThreshold)} must be greater than 0 when set (was {versionThreshold}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
