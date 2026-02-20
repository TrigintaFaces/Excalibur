// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Views;

/// <summary>
/// Validates <see cref="MaterializedViewOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
public sealed class MaterializedViewOptionsValidator : IValidateOptions<MaterializedViewOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MaterializedViewOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.BatchSize is < 1 or > 10000)
		{
			failures.Add($"{nameof(MaterializedViewOptions.BatchSize)} must be between 1 and 10000 (was {options.BatchSize}).");
		}

		if (options.BatchDelay < TimeSpan.Zero)
		{
			failures.Add($"{nameof(MaterializedViewOptions.BatchDelay)} must not be negative (was {options.BatchDelay}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
