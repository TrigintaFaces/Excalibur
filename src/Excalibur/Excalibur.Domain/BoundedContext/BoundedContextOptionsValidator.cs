// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Domain.BoundedContext;

/// <summary>Validates <see cref="BoundedContextOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class BoundedContextOptionsValidator : IValidateOptions<BoundedContextOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, BoundedContextOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return !Enum.IsDefined(options.EnforcementMode)
			? ValidateOptionsResult.Fail($"{nameof(BoundedContextOptions.EnforcementMode)} must be a defined enforcement mode.")
			: ValidateOptionsResult.Success;
	}
}
