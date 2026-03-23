// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.Postgres;

/// <summary>
/// Validates <see cref="PostgresInboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class PostgresInboxOptionsValidator : IValidateOptions<PostgresInboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, PostgresInboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Postgres inbox options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"PostgresInboxOptions.ConnectionString is required. Provide a valid Postgres connection string.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			return ValidateOptionsResult.Fail(
				"PostgresInboxOptions.SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			return ValidateOptionsResult.Fail(
				"PostgresInboxOptions.TableName is required.");
		}

		if (options.CommandTimeoutSeconds < 1)
		{
			return ValidateOptionsResult.Fail(
				"PostgresInboxOptions.CommandTimeoutSeconds must be at least 1.");
		}

		if (options.MaxRetryCount < 0)
		{
			return ValidateOptionsResult.Fail(
				"PostgresInboxOptions.MaxRetryCount must be zero or greater.");
		}

		return ValidateOptionsResult.Success;
	}
}
