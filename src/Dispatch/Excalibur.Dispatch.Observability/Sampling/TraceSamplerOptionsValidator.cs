// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sampling;

/// <summary>
/// AOT-safe validator for <see cref="TraceSamplerOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class TraceSamplerOptionsValidator : IValidateOptions<TraceSamplerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TraceSamplerOptions options)
	{
		if (options.SamplingRatio is < 0.0 or > 1.0)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.SamplingRatio)} must be between 0.0 and 1.0.");
		}

		return ValidateOptionsResult.Success;
	}
}
