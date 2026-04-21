// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.Postgres;

/// <summary>
/// Validates <see cref="PostgresInboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Supports builder-configured connections that don't set ConnectionString on the options object.
/// </summary>
internal sealed class PostgresInboxOptionsValidator : IValidateOptions<PostgresInboxOptions>
{
	/// <summary>
	/// Gets or sets a value indicating whether a builder-level connection was configured.
	/// When true, ConnectionString validation is skipped.
	/// </summary>
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, PostgresInboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Postgres inbox options cannot be null.");
		}

		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"No connection configured for Inbox (Postgres). " +
				"Call ConnectionString(), ConnectionStringName(), DataSource(), DataSourceFactory(), " +
				"or BindConfiguration() inside UsePostgres().");
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
