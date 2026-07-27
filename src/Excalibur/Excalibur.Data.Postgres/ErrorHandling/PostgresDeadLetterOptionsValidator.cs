// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.ErrorHandling;

/// <summary>Validates <see cref="PostgresDeadLetterOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class PostgresDeadLetterOptionsValidator : IValidateOptions<PostgresDeadLetterOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PostgresDeadLetterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(PostgresDeadLetterOptions.ConnectionString)} must be a non-empty connection string.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			failures.Add($"{nameof(PostgresDeadLetterOptions.SchemaName)} must be a non-empty schema name.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			failures.Add($"{nameof(PostgresDeadLetterOptions.TableName)} must be a non-empty table name.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
