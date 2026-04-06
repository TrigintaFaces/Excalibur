// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Validates <see cref="AspNetCoreAuthorizationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AspNetCoreAuthorizationOptionsValidator : IValidateOptions<AspNetCoreAuthorizationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AspNetCoreAuthorizationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// All properties have safe defaults. No cross-property constraints to enforce.
		return ValidateOptionsResult.Success;
	}
}
