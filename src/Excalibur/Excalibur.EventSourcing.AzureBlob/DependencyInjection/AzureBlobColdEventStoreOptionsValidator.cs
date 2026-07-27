// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.AzureBlob.DependencyInjection;

/// <summary>
/// Validates <see cref="AzureBlobColdEventStoreOptions"/> at startup so a misconfigured cold store fails fast
/// instead of surfacing as a deep runtime error on the first archive operation.
/// </summary>
internal sealed class AzureBlobColdEventStoreOptionsValidator : IValidateOptions<AzureBlobColdEventStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AzureBlobColdEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AzureBlobColdEventStoreOptions.ConnectionString)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.ContainerName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AzureBlobColdEventStoreOptions.ContainerName)} is required and must not be empty or whitespace.");
		}

		return ValidateOptionsResult.Success;
	}
}
