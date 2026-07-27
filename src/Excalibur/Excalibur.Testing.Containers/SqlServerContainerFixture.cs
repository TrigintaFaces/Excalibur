// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

namespace Excalibur.Testing.Containers;

/// <summary>
/// A reusable SQL Server <see cref="IDatabaseContainerFixture"/> backed by a TestContainers
/// <see cref="MsSqlContainer"/>. Inherit or use directly to test a SQL Server provider implementation
/// against a real engine.
/// </summary>
public class SqlServerContainerFixture : ContainerFixtureBase, IDatabaseContainerFixture
{
	private MsSqlContainer? _container;

	/// <inheritdoc />
	public string ConnectionString =>
		_container?.GetConnectionString()
		?? throw new InvalidOperationException("The SQL Server container has not been initialized.");

	/// <inheritdoc />
	public DatabaseEngine Engine => DatabaseEngine.SqlServer;

	/// <inheritdoc />
	public IDbConnection CreateDbConnection() => new SqlConnection(ConnectionString);

	/// <summary>Gets the Docker image used for the SQL Server container.</summary>
	protected virtual string Image => "mcr.microsoft.com/mssql/server:2022-latest";

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MsSqlBuilder().WithImage(Image).Build();
		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
