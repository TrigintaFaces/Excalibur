// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Npgsql;

using Testcontainers.PostgreSql;

namespace Excalibur.Testing.Containers;

/// <summary>
/// A reusable PostgreSQL <see cref="IDatabaseContainerFixture"/> backed by a TestContainers
/// <see cref="PostgreSqlContainer"/>. Inherit or use directly to test a PostgreSQL provider implementation
/// against a real engine.
/// </summary>
public class PostgresContainerFixture : ContainerFixtureBase, IDatabaseContainerFixture
{
	private PostgreSqlContainer? _container;

	/// <inheritdoc />
	public string ConnectionString =>
		_container?.GetConnectionString()
		?? throw new InvalidOperationException("The PostgreSQL container has not been initialized.");

	/// <inheritdoc />
	public DatabaseEngine Engine => DatabaseEngine.Postgres;

	/// <inheritdoc />
	public IDbConnection CreateDbConnection() => new NpgsqlConnection(ConnectionString);

	/// <summary>Gets the Docker image used for the PostgreSQL container.</summary>
	protected virtual string Image => "postgres:16-alpine";

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder().WithImage(Image).Build();
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
