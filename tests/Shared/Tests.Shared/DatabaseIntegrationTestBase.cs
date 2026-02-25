// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Tests.Shared;

/// <summary>
/// Base class for database integration tests using TestContainers.
/// </summary>
/// <typeparam name="TFixture">The database fixture type (e.g., SqlServerFixture, PostgresFixture).</typeparam>
public abstract class DatabaseIntegrationTestBase<TFixture> : IntegrationTestBase, IClassFixture<TFixture>
	where TFixture : class, IAsyncLifetime
{
	protected DatabaseIntegrationTestBase(TFixture fixture)
	{
		Fixture = fixture;
	}

	protected TFixture Fixture { get; }

	/// <summary>
	/// Gets the connection string from the fixture.
	/// Override in derived classes to get from the specific fixture type.
	/// </summary>
	protected abstract string ConnectionString { get; }

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await SetupDatabaseAsync();
	}

	public override async Task DisposeAsync()
	{
		await CleanupDatabaseAsync();
		await base.DisposeAsync();
	}

	/// <summary>
	/// Initialize services and build provider. Call in derived class constructor after setting up services.
	/// </summary>
	protected void InitializeServices()
	{
		ConfigureServices(Services);
		BuildServiceProvider();
	}

	/// <summary>
	/// Configure database-specific services.
	/// </summary>
	protected virtual void ConfigureServices(IServiceCollection services)
	{
		// Override in derived classes to register DbContext or other DB services
	}

	/// <summary>
	/// Execute database setup scripts before tests.
	/// </summary>
	protected virtual Task SetupDatabaseAsync() => Task.CompletedTask;

	/// <summary>
	/// Clean up test data after tests.
	/// </summary>
	protected virtual Task CleanupDatabaseAsync() => Task.CompletedTask;
}

/// <summary>
/// SQL Server integration test base class.
/// </summary>
public abstract class SqlServerIntegrationTestBase : DatabaseIntegrationTestBase<SqlServerContainerFixture>
{
	protected SqlServerIntegrationTestBase(SqlServerContainerFixture fixture) : base(fixture)
	{
	}

	protected override string ConnectionString => Fixture.ConnectionString;
}

/// <summary>
/// Postgres integration test base class.
/// </summary>
public abstract class PostgresIntegrationTestBase : DatabaseIntegrationTestBase<PostgresContainerFixture>
{
	protected PostgresIntegrationTestBase(PostgresContainerFixture fixture) : base(fixture)
	{
	}

	protected override string ConnectionString => Fixture.ConnectionString;
}

/// <summary>
/// MongoDB integration test base class.
/// </summary>
public abstract class MongoDbIntegrationTestBase : DatabaseIntegrationTestBase<MongoDbContainerFixture>
{
	protected MongoDbIntegrationTestBase(MongoDbContainerFixture fixture) : base(fixture)
	{
	}

	protected override string ConnectionString => Fixture.ConnectionString;
}
