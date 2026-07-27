// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>Validates <see cref="OrphanedAccessOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class OrphanedAccessOptionsValidator : IValidateOptions<OrphanedAccessOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, OrphanedAccessOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.ScanIntervalHours is < 1 or > 8760)
		{
			failures.Add($"{nameof(OrphanedAccessOptions.ScanIntervalHours)} must be between 1 and 8760.");
		}

		if (options.InactiveGracePeriodDays is < 1 or > 365)
		{
			failures.Add($"{nameof(OrphanedAccessOptions.InactiveGracePeriodDays)} must be between 1 and 365.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
