// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.SqlServer;

/// <summary>
/// AOT-safe validator for <see cref="SqlServerAuditOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class SqlServerAuditOptionsValidator : IValidateOptions<SqlServerAuditOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SqlServerAuditOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail($"{nameof(options.ConnectionString)} is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
