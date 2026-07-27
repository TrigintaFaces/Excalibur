// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Annotation;

/// <summary>
/// Validates <see cref="AuditContextOptions"/> at startup. Reflection-free (AOT-safe).
/// </summary>
internal sealed class AuditContextOptionsValidator : IValidateOptions<AuditContextOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AuditContextOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.MaxAssertionsPerScope is < 1 or > 1000
			? ValidateOptionsResult.Fail(
				$"{nameof(AuditContextOptions.MaxAssertionsPerScope)} must be between 1 and 1000.")
			: ValidateOptionsResult.Success;
	}
}
