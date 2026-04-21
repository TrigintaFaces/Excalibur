// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ClaimCheck.AwsS3;

/// <summary>
/// Validates <see cref="AwsS3ClaimCheckOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class AwsS3ClaimCheckOptionsValidator : IValidateOptions<AwsS3ClaimCheckOptions>
{
	private const long MaxS3ObjectSize = 5L * 1024 * 1024 * 1024;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsS3ClaimCheckOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.BucketName))
		{
			failures.Add($"{nameof(AwsS3ClaimCheckOptions.BucketName)} is required.");
		}

		if (options.MaxObjectSize is < 1 or > MaxS3ObjectSize)
		{
			failures.Add($"{nameof(AwsS3ClaimCheckOptions.MaxObjectSize)} must be between 1 and {MaxS3ObjectSize} (was {options.MaxObjectSize}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
