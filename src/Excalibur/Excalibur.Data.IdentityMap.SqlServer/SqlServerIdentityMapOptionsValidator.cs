// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.IdentityMap.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerIdentityMapOptions"/> at startup via ValidateOnStart.
/// Ensures a connection has been configured through the builder.
/// </summary>
internal sealed partial class SqlServerIdentityMapOptionsValidator : IValidateOptions<SqlServerIdentityMapOptions>
{
	/// <summary>
	/// Gets or sets a value indicating whether the builder configured a connection
	/// via <see cref="Builders.ISqlServerIdentityMapBuilder.ConnectionFactory"/> or
	/// <see cref="Builders.ISqlServerIdentityMapBuilder.ConnectionStringName"/>.
	/// </summary>
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerIdentityMapOptions options)
	{
		var failures = new List<string>();

		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add(
				"No connection configured for IdentityMap. " +
				"Call ConnectionString(), ConnectionStringName(), ConnectionFactory(), " +
				"or BindConfiguration() inside UseSqlServer().");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			failures.Add("SchemaName is required.");
		}
		else if (!SafeIdentifierRegex().IsMatch(options.SchemaName))
		{
			failures.Add("SchemaName contains invalid characters.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			failures.Add("TableName is required.");
		}
		else if (!SafeIdentifierRegex().IsMatch(options.TableName))
		{
			failures.Add("TableName contains invalid characters.");
		}

		if (options.CommandTimeoutSeconds <= 0)
		{
			failures.Add("CommandTimeoutSeconds must be positive.");
		}

		if (options.MaxBatchSize <= 0)
		{
			failures.Add("MaxBatchSize must be positive.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SafeIdentifierRegex();
}
