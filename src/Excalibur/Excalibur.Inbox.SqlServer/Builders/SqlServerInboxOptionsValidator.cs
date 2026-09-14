// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerInboxOptions"/> at startup via ValidateOnStart.
/// Ensures a connection has been configured through the builder.
/// </summary>
internal sealed class SqlServerInboxBuilderOptionsValidator : IValidateOptions<SqlServerInboxOptions>
{
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerInboxOptions options)
	{
		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"No connection configured for Inbox. " +
				"Call ConnectionString(), ConnectionStringName(), or ConnectionFactory() inside " +
				"UseSqlServer(), or bind the options yourself with " +
				"services.AddOptions<SqlServerInboxOptions>().BindConfiguration(\"Section:Path\").");
		}

		return ValidateOptionsResult.Success;
	}
}
