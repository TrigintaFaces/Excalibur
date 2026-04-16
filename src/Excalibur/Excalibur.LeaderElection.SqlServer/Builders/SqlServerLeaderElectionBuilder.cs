// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// Internal implementation of the SQL Server leader election builder.
/// </summary>
/// <remarks>
/// <para>
/// Connection overloads use <b>last-wins</b> semantics: each connection method
/// clears any previously configured connection state to prevent ambiguity.
/// </para>
/// <para>
/// Feature-specific methods (<see cref="LockResource"/>) are additive and do not
/// clear connection state.
/// </para>
/// </remarks>
internal sealed class SqlServerLeaderElectionBuilder : ISqlServerLeaderElectionBuilder
{
	private readonly SqlServerLeaderElectionOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerLeaderElectionBuilder"/> class.
	/// </summary>
	/// <param name="options">The SQL Server leader election options to configure.</param>
	internal SqlServerLeaderElectionBuilder(SqlServerLeaderElectionOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>
	/// Gets the connection factory, if configured via <see cref="ConnectionFactory"/>.
	/// </summary>
	internal Func<IServiceProvider, Func<SqlConnection>>? ConnectionFactoryFunc { get; private set; }

	/// <summary>
	/// Gets the connection string name for resolution from IConfiguration.
	/// </summary>
	internal string? ConnectionStringNameValue { get; private set; }

	/// <summary>
	/// Gets the BindConfiguration section path, if set.
	/// </summary>
	internal string? BindConfigurationPath { get; private set; }

	// --- Connection overloads (last-wins: each clears the others) ---

	/// <inheritdoc/>
	public ISqlServerLeaderElectionBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerLeaderElectionBuilder ConnectionFactory(
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		ConnectionFactoryFunc = connectionFactory;
		_options.ConnectionString = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerLeaderElectionBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerLeaderElectionBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		return this;
	}

	// --- Feature-specific configuration ---

	/// <inheritdoc/>
	public ISqlServerLeaderElectionBuilder LockResource(string lockResource)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockResource);

		_options.LockResource = lockResource;
		return this;
	}
}
