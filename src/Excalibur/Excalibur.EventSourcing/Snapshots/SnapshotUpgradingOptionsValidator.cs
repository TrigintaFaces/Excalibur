// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Validates <see cref="SnapshotUpgradingOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
public sealed class SnapshotUpgradingOptionsValidator : IValidateOptions<SnapshotUpgradingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SnapshotUpgradingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.CurrentSnapshotVersion < 1)
		{
			failures.Add($"{nameof(SnapshotUpgradingOptions.CurrentSnapshotVersion)} must be at least 1 (was {options.CurrentSnapshotVersion}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
