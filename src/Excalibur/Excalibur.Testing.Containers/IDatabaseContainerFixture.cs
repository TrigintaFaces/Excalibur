// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Xunit;

namespace Excalibur.Testing.Containers;

/// <summary>
/// A database container fixture that provides connection capabilities. Extends the xUnit
/// <see cref="IAsyncLifetime"/> pattern with database-specific connection and availability members.
/// </summary>
public interface IDatabaseContainerFixture : IAsyncLifetime
{
	/// <summary>
	/// Gets the connection string for the database container. Valid only after
	/// <see cref="IAsyncLifetime.InitializeAsync"/> completes and <see cref="DockerAvailable"/> is
	/// <see langword="true"/>.
	/// </summary>
	string ConnectionString { get; }

	/// <summary>Gets the database engine provided by this fixture.</summary>
	DatabaseEngine Engine { get; }

	/// <summary>
	/// Gets a value indicating whether Docker is available and the container started successfully.
	/// </summary>
	bool DockerAvailable { get; }

	/// <summary>Gets the initialization error message if container startup failed; otherwise <see langword="null"/>.</summary>
	string? InitializationError { get; }

	/// <summary>
	/// Creates a new, unopened <see cref="IDbConnection"/> to the container database.
	/// </summary>
	/// <returns>An unopened connection; call <see cref="IDbConnection.Open"/> before use.</returns>
	IDbConnection CreateDbConnection();
}
