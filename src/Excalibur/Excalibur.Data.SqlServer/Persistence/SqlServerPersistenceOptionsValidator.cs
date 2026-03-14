// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Validates <see cref="SqlServerPersistenceOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks including:
/// <list type="bullet">
///   <item><description>MinPoolSize must not exceed MaxPoolSize when connection pooling is enabled</description></item>
///   <item><description>ConnectionTimeout must be positive</description></item>
///   <item><description>CommandTimeout must be positive</description></item>
///   <item><description>ConnectionString must not be empty</description></item>
/// </list>
/// </remarks>
internal sealed class SqlServerPersistenceOptionsValidator : IValidateOptions<SqlServerPersistenceOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SqlServerPersistenceOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(SqlServerPersistenceOptions.ConnectionString)} must not be null or whitespace.");
		}

		if (options.Connection.ConnectionTimeout <= 0)
		{
			failures.Add(
				$"{nameof(SqlServerPersistenceOptions.Connection)}.{nameof(SqlServerConnectionOptions.ConnectionTimeout)} must be greater than 0 (was {options.Connection.ConnectionTimeout}).");
		}

		if (options.CommandTimeout <= 0)
		{
			failures.Add(
				$"{nameof(SqlServerPersistenceOptions.CommandTimeout)} must be greater than 0 (was {options.CommandTimeout}).");
		}

		if (options.Pooling.EnableConnectionPooling && options.Pooling.MinPoolSize > options.Pooling.MaxPoolSize)
		{
			failures.Add(
				$"{nameof(SqlServerPersistenceOptions.Pooling)}.{nameof(SqlServerPoolingOptions.MinPoolSize)} ({options.Pooling.MinPoolSize}) " +
				$"must not exceed {nameof(SqlServerPersistenceOptions.Pooling)}.{nameof(SqlServerPoolingOptions.MaxPoolSize)} ({options.Pooling.MaxPoolSize}) " +
				$"when connection pooling is enabled.");
		}

		if (options.Pooling.EnableConnectionPooling && options.Pooling.MaxPoolSize <= 0)
		{
			failures.Add(
				$"{nameof(SqlServerPersistenceOptions.Pooling)}.{nameof(SqlServerPoolingOptions.MaxPoolSize)} must be greater than 0 when connection pooling is enabled (was {options.Pooling.MaxPoolSize}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
