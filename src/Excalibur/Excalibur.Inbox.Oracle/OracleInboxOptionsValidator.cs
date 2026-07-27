// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.Oracle;

/// <summary>
/// Validates <see cref="OracleInboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed partial class OracleInboxOptionsValidator : IValidateOptions<OracleInboxOptions>
{
	// Allowlist for SQL identifiers interpolated into DML (schema/table names): ASCII letters,
	// digits, underscores only — prevents SQL injection via a configured identifier. AOT-safe
	// generated regex (no RegexOptions.Compiled).
	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex ValidIdentifierPattern();

	/// <summary>
	/// Gets a value indicating whether a builder-level connection was configured.
	/// When true, ConnectionString validation is skipped.
	/// </summary>
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OracleInboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Oracle inbox options cannot be null.");
		}

		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"No connection configured for Inbox (Oracle). Set OracleInboxOptions.ConnectionString.");
		}

		// SchemaName is optional for Oracle (the connection's default schema is used when empty),
		// but when supplied it must be a safe identifier.
		if (!string.IsNullOrWhiteSpace(options.SchemaName) && !ValidIdentifierPattern().IsMatch(options.SchemaName))
		{
			return ValidateOptionsResult.Fail(
				"OracleInboxOptions.SchemaName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			return ValidateOptionsResult.Fail("OracleInboxOptions.TableName is required.");
		}

		if (!ValidIdentifierPattern().IsMatch(options.TableName))
		{
			return ValidateOptionsResult.Fail(
				"OracleInboxOptions.TableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (options.CommandTimeoutSeconds < 1)
		{
			return ValidateOptionsResult.Fail("OracleInboxOptions.CommandTimeoutSeconds must be at least 1.");
		}

		if (options.MaxRetryCount < 0)
		{
			return ValidateOptionsResult.Fail("OracleInboxOptions.MaxRetryCount must be zero or greater.");
		}

		return ValidateOptionsResult.Success;
	}
}
