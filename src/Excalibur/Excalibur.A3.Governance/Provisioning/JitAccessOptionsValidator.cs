// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Cross-property validator for <see cref="JitAccessOptions"/>.
/// </summary>
internal sealed class JitAccessOptionsValidator : IValidateOptions<JitAccessOptions>
{
	public ValidateOptionsResult Validate(string? name, JitAccessOptions options)
	{
		if (options.DefaultJitDuration > options.MaxJitDuration)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(JitAccessOptions.DefaultJitDuration)} ({options.DefaultJitDuration}) " +
				$"must not exceed {nameof(JitAccessOptions.MaxJitDuration)} ({options.MaxJitDuration}).");
		}

		if (options.DefaultJitDuration <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(JitAccessOptions.DefaultJitDuration)} must be positive.");
		}

		if (options.ExpiryCheckInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(JitAccessOptions.ExpiryCheckInterval)} must be positive.");
		}

		return ValidateOptionsResult.Success;
	}
}
