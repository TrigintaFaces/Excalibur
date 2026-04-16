// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerEventSourcingOptions"/> at startup via ValidateOnStart.
/// Ensures a connection has been configured through the builder.
/// </summary>
internal sealed class SqlServerEventSourcingOptionsValidator : IValidateOptions<SqlServerEventSourcingOptions>
{
	/// <summary>
	/// Marker key used to indicate that a non-connection-string connection method
	/// (factory, connection string name) was configured via the builder.
	/// When this marker is set, the connection string is not required on the options.
	/// </summary>
	internal const string BuilderConnectionConfiguredKey = "__BuilderConnectionConfigured__";

	/// <summary>
	/// Gets or sets a value indicating whether the builder configured a connection
	/// via <see cref="ISqlServerEventSourcingBuilder.ConnectionFactory"/> or
	/// <see cref="ISqlServerEventSourcingBuilder.ConnectionStringName"/>.
	/// </summary>
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerEventSourcingOptions options)
	{
		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"No connection configured for EventSourcing. " +
				"Call ConnectionString(), ConnectionStringName(), ConnectionFactory(), " +
				"or BindConfiguration() inside UseSqlServer().");
		}

		return ValidateOptionsResult.Success;
	}
}
