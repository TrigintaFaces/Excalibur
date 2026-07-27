// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.AwsS3.DependencyInjection;

/// <summary>
/// Validates <see cref="AwsS3ColdEventStoreOptions"/> at startup so a misconfigured cold store fails fast
/// instead of surfacing as a deep runtime error on the first archive operation.
/// </summary>
internal sealed class AwsS3ColdEventStoreOptionsValidator : IValidateOptions<AwsS3ColdEventStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsS3ColdEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.BucketName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AwsS3ColdEventStoreOptions.BucketName)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.KeyPrefix))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AwsS3ColdEventStoreOptions.KeyPrefix)} must not be empty or whitespace.");
		}

		return ValidateOptionsResult.Success;
	}
}
