// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.IdentityMap.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerIdentityMapOptions"/> at startup.
/// </summary>
internal sealed partial class SqlServerIdentityMapOptionsValidator : IValidateOptions<SqlServerIdentityMapOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerIdentityMapOptions options)
	{
		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add("ConnectionString is required.");
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
