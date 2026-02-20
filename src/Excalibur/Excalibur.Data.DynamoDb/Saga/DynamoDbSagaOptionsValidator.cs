// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Saga;

/// <summary>
/// Validates <see cref="DynamoDbSagaOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Performs cross-property constraint checks beyond what <see cref="System.ComponentModel.DataAnnotations"/> can express.
/// </summary>
public sealed class DynamoDbSagaOptionsValidator : IValidateOptions<DynamoDbSagaOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DynamoDbSagaOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// Either ServiceUrl (local dev) or Region (AWS) must be provided
		var hasLocalConfig = !string.IsNullOrWhiteSpace(options.ServiceUrl);
		var hasAwsConfig = !string.IsNullOrWhiteSpace(options.Region);

		if (!hasLocalConfig && !hasAwsConfig)
		{
			failures.Add(
				$"Either {nameof(DynamoDbSagaOptions.ServiceUrl)} (for local development) or " +
				$"{nameof(DynamoDbSagaOptions.Region)} (for AWS) must be provided.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			failures.Add($"{nameof(DynamoDbSagaOptions.TableName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TtlAttributeName))
		{
			failures.Add($"{nameof(DynamoDbSagaOptions.TtlAttributeName)} is required.");
		}

		// Cross-property: if AccessKey is provided, SecretKey should also be provided (and vice versa)
		var hasAccessKey = !string.IsNullOrWhiteSpace(options.AccessKey);
		var hasSecretKey = !string.IsNullOrWhiteSpace(options.SecretKey);

		if (hasAccessKey != hasSecretKey)
		{
			failures.Add(
				$"When {nameof(DynamoDbSagaOptions.AccessKey)} is provided, " +
				$"{nameof(DynamoDbSagaOptions.SecretKey)} must also be provided (and vice versa).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
