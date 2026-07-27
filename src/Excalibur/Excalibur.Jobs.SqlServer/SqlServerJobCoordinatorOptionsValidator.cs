// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerJobCoordinatorOptions"/> at startup. Reflection-free (AOT-safe) so the
/// guarantees hold on trimmed/AOT consumer applications.
/// </summary>
internal sealed class SqlServerJobCoordinatorOptionsValidator : IValidateOptions<SqlServerJobCoordinatorOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerJobCoordinatorOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(SqlServerJobCoordinatorOptions.ConnectionString)} must be a non-empty connection string.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			failures.Add($"{nameof(SqlServerJobCoordinatorOptions.SchemaName)} must be a non-empty schema name.");
		}

		if (options.LockTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(SqlServerJobCoordinatorOptions.LockTimeout)} must be greater than zero.");
		}

		if (options.InstanceTtl <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(SqlServerJobCoordinatorOptions.InstanceTtl)} must be greater than zero.");
		}

		if (options.CompletionRetention <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(SqlServerJobCoordinatorOptions.CompletionRetention)} must be greater than zero.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
