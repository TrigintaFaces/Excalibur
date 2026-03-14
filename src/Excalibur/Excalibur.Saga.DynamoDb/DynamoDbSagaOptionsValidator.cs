// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.DynamoDb;

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
		var hasLocalConfig = !string.IsNullOrWhiteSpace(options.Connection.ServiceUrl);
		var hasAwsConfig = !string.IsNullOrWhiteSpace(options.Connection.Region);

		if (!hasLocalConfig && !hasAwsConfig)
		{
			failures.Add(
				$"Either {nameof(DynamoDbSagaOptions)}.{nameof(DynamoDbSagaOptions.Connection)}.{nameof(DynamoDbConnectionOptions.ServiceUrl)} (for local development) or " +
				$"{nameof(DynamoDbSagaOptions)}.{nameof(DynamoDbSagaOptions.Connection)}.{nameof(DynamoDbConnectionOptions.Region)} (for AWS) must be provided.");
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
		var hasAccessKey = !string.IsNullOrWhiteSpace(options.Connection.AccessKey);
		var hasSecretKey = !string.IsNullOrWhiteSpace(options.Connection.SecretKey);

		if (hasAccessKey != hasSecretKey)
		{
			failures.Add(
				$"When {nameof(DynamoDbSagaOptions)}.{nameof(DynamoDbSagaOptions.Connection)}.{nameof(DynamoDbConnectionOptions.AccessKey)} is provided, " +
				$"{nameof(DynamoDbSagaOptions)}.{nameof(DynamoDbSagaOptions.Connection)}.{nameof(DynamoDbConnectionOptions.SecretKey)} must also be provided (and vice versa).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
