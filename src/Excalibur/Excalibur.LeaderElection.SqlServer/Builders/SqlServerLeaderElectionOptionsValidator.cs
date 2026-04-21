// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerLeaderElectionOptions"/> at startup via ValidateOnStart.
/// Ensures a connection and lock resource have been configured through the builder.
/// </summary>
internal sealed class SqlServerLeaderElectionOptionsValidator : IValidateOptions<SqlServerLeaderElectionOptions>
{
	/// <summary>
	/// Gets or sets a value indicating whether the builder configured a connection
	/// via <see cref="ISqlServerLeaderElectionBuilder.ConnectionFactory"/> or
	/// <see cref="ISqlServerLeaderElectionBuilder.ConnectionStringName"/>.
	/// </summary>
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerLeaderElectionOptions options)
	{
		var failures = new List<string>();

		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add(
				"No connection configured for LeaderElection. " +
				"Call ConnectionString(), ConnectionStringName(), ConnectionFactory(), " +
				"or BindConfiguration() inside UseSqlServer().");
		}

		if (string.IsNullOrWhiteSpace(options.LockResource))
		{
			failures.Add(
				"No lock resource configured for LeaderElection. " +
				"Call LockResource(\"MyApp.Leader\") inside UseSqlServer().");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
