// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System.Data;

namespace Tests.Shared.Fixtures;

/// <summary>
/// Interface for database container fixtures that provide connection capabilities.
/// </summary>
/// <remarks>
/// This interface extends the xUnit <see cref="IAsyncLifetime"/> pattern and adds
/// database-specific properties for connection management and Docker availability checking.
/// </remarks>
public interface IDatabaseContainerFixture : IAsyncLifetime
{
	/// <summary>
	/// Gets the connection string for the database container.
	/// </summary>
	/// <remarks>
	/// This property is only valid after <see cref="IAsyncLifetime.InitializeAsync"/> completes
	/// and when <see cref="DockerAvailable"/> is <c>true</c>.
	/// </remarks>
	string ConnectionString { get; }

	/// <summary>
	/// Gets the type of database engine provided by this fixture.
	/// </summary>
	DatabaseEngine Engine { get; }

	/// <summary>
	/// Gets a value indicating whether Docker is available and the container started successfully.
	/// </summary>
	/// <remarks>
	/// Docker is expected to be available in all CI environments.
	/// If container initialization fails, xUnit fixture initialization throws.
	/// </remarks>
	bool DockerAvailable { get; }

	/// <summary>
	/// Gets the initialization error message if container startup failed.
	/// </summary>
	/// <remarks>
	/// This property provides context for why <see cref="DockerAvailable"/> is <c>false</c>.
	/// </remarks>
	string? InitializationError { get; }

	/// <summary>
	/// Creates a new database connection to the container.
	/// </summary>
	/// <returns>An unopened <see cref="IDbConnection"/> to the database.</returns>
	/// <remarks>
	/// The returned connection is not opened. Call <see cref="IDbConnection.Open"/> before use.
	/// </remarks>
	IDbConnection CreateDbConnection();
}
