// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.SqlServer;

/// <summary>
/// AOT-safe validator for <see cref="SqlServerAuditAnnotationStoreOptions"/>.
/// </summary>
internal sealed class SqlServerAuditAnnotationStoreOptionsValidator
	: IValidateOptions<SqlServerAuditAnnotationStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SqlServerAuditAnnotationStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add(
				$"{nameof(SqlServerAuditAnnotationStoreOptions)}.{nameof(options.ConnectionString)} is required. " +
				$"Configure it via services.Configure<{nameof(SqlServerAuditAnnotationStoreOptions)}>(o => o.ConnectionString = \"...\").");
		}

		if (options.CommandTimeoutSeconds < 1)
		{
			failures.Add(
				$"{nameof(SqlServerAuditAnnotationStoreOptions)}.{nameof(options.CommandTimeoutSeconds)} must be >= 1 (was {options.CommandTimeoutSeconds}).");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			failures.Add(
				$"{nameof(SqlServerAuditAnnotationStoreOptions)}.{nameof(options.SchemaName)} must not be empty.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			failures.Add(
				$"{nameof(SqlServerAuditAnnotationStoreOptions)}.{nameof(options.TableName)} must not be empty.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
