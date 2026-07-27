// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Stores.Postgres;

/// <summary>Validates <see cref="PostgresComplianceOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class PostgresComplianceOptionsValidator : IValidateOptions<PostgresComplianceOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PostgresComplianceOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(PostgresComplianceOptions.ConnectionString)} must be a non-empty connection string.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			failures.Add($"{nameof(PostgresComplianceOptions.SchemaName)} must be a non-empty schema name.");
		}

		if (options.CommandTimeoutSeconds < 1)
		{
			failures.Add($"{nameof(PostgresComplianceOptions.CommandTimeoutSeconds)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
