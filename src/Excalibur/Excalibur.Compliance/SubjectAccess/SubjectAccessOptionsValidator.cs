// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>Validates <see cref="SubjectAccessOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class SubjectAccessOptionsValidator : IValidateOptions<SubjectAccessOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SubjectAccessOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.ResponseDeadlineDays < 1
			? ValidateOptionsResult.Fail($"{nameof(SubjectAccessOptions.ResponseDeadlineDays)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
