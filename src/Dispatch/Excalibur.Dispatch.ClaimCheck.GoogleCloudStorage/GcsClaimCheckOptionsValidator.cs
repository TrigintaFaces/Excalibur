// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;

/// <summary>
/// Validates <see cref="GcsClaimCheckOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class GcsClaimCheckOptionsValidator : IValidateOptions<GcsClaimCheckOptions>
{
	private const long MaxGcsObjectSize = 5L * 1024 * 1024 * 1024;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GcsClaimCheckOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.BucketName))
		{
			failures.Add($"{nameof(GcsClaimCheckOptions.BucketName)} is required.");
		}

		if (options.MaxObjectSize is < 1 or > MaxGcsObjectSize)
		{
			failures.Add($"{nameof(GcsClaimCheckOptions.MaxObjectSize)} must be between 1 and {MaxGcsObjectSize} (was {options.MaxObjectSize}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
