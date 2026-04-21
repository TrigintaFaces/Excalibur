// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.OpenSearch.Projections;

/// <summary>
/// Validates <see cref="OpenSearchProjectionStoreOptions"/> at startup.
/// </summary>
internal sealed class OpenSearchProjectionStoreOptionsValidator
	: IValidateOptions<OpenSearchProjectionStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OpenSearchProjectionStoreOptions options)
	{
		if (options.NodeUris is { Count: > 0 })
		{
			foreach (var uri in options.NodeUris)
			{
				if (uri is null || !uri.IsAbsoluteUri)
				{
					return ValidateOptionsResult.Fail(
						$"All entries in NodeUris must be valid absolute URIs. Invalid: '{uri}'.");
				}
			}
		}
		else if (string.IsNullOrWhiteSpace(options.NodeUri))
		{
			return ValidateOptionsResult.Fail("NodeUri is required when NodeUris is not set.");
		}
		else if (!Uri.TryCreate(options.NodeUri, UriKind.Absolute, out _))
		{
			return ValidateOptionsResult.Fail($"NodeUri '{options.NodeUri}' is not a valid URI.");
		}

		if (options.RequestTimeoutSeconds <= 0)
		{
			return ValidateOptionsResult.Fail("RequestTimeoutSeconds must be greater than 0.");
		}

		if (options.NumberOfShards < 1)
		{
			return ValidateOptionsResult.Fail("NumberOfShards must be at least 1.");
		}

		if (options.NumberOfReplicas < 0)
		{
			return ValidateOptionsResult.Fail("NumberOfReplicas must be non-negative.");
		}

		return ValidateOptionsResult.Success;
	}
}
