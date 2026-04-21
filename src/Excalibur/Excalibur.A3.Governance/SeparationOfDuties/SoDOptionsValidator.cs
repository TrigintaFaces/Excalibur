// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Validates <see cref="SoDOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces <c>[Range(typeof(TimeSpan), ...)]</c> attribute with an AOT-safe check.
/// </summary>
internal sealed class SoDOptionsValidator : IValidateOptions<SoDOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SoDOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.DetectiveScanInterval < TimeSpan.FromMinutes(1) || options.DetectiveScanInterval > TimeSpan.FromDays(7))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(SoDOptions.DetectiveScanInterval)} must be between 00:01:00 and 168.00:00:00 (was {options.DetectiveScanInterval}).");
		}

		return ValidateOptionsResult.Success;
	}
}
