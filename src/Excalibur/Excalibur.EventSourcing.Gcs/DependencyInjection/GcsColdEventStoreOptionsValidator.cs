// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Gcs.DependencyInjection;

/// <summary>
/// Validates <see cref="GcsColdEventStoreOptions"/> at startup so a misconfigured cold store fails fast
/// instead of surfacing as a deep runtime error on the first archive operation.
/// </summary>
internal sealed class GcsColdEventStoreOptionsValidator : IValidateOptions<GcsColdEventStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GcsColdEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.BucketName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GcsColdEventStoreOptions.BucketName)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.ObjectPrefix))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GcsColdEventStoreOptions.ObjectPrefix)} must not be empty or whitespace.");
		}

		return ValidateOptionsResult.Success;
	}
}
