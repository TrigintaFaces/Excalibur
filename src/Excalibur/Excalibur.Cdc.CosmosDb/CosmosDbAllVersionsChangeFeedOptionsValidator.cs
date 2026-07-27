// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Validates <see cref="CosmosDbAllVersionsChangeFeedOptions"/> at startup so a misconfigured change-feed
/// processor fails fast instead of surfacing as a deep runtime error when the processor starts.
/// </summary>
internal sealed class CosmosDbAllVersionsChangeFeedOptionsValidator
	: IValidateOptions<CosmosDbAllVersionsChangeFeedOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CosmosDbAllVersionsChangeFeedOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.LeaseContainer))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbAllVersionsChangeFeedOptions.LeaseContainer)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.ProcessorName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbAllVersionsChangeFeedOptions.ProcessorName)} is required and must not be empty or whitespace.");
		}

		if (options.FeedPollInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbAllVersionsChangeFeedOptions.FeedPollInterval)} must be greater than zero.");
		}

		if (options.MaxBatchSize < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbAllVersionsChangeFeedOptions.MaxBatchSize)} must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
