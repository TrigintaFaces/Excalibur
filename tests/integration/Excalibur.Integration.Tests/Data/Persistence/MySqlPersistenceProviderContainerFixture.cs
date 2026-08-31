// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.MySql;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// MySQL container fixture for the MySQL persistence-provider conformance suite.
/// </summary>
/// <remarks>
/// Extends <see cref="ContainerFixtureBase"/>: real-infra conformance is never skipped, so a missing
/// container surfaces as a failure rather than a silent pass.
/// </remarks>
public sealed class MySqlPersistenceProviderContainerFixture : ContainerFixtureBase
{
	private MySqlContainer? _container;

	/// <summary>
	/// Gets the connection string for the running MySQL container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MySqlBuilder()
			.WithImage("mysql:8.0")
			.WithName($"mysql-persistence-{Guid.NewGuid():N}")
			.WithDatabase("persistence_conformance")
			.WithUsername("excalibur")
			.WithPassword("excalibur")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}

/// <summary>
/// xUnit collection definition for the MySQL persistence-provider conformance suite.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class MySqlPersistenceProviderTestCollection
	: ICollectionFixture<MySqlPersistenceProviderContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "MySql Persistence Provider Integration Tests";
}
