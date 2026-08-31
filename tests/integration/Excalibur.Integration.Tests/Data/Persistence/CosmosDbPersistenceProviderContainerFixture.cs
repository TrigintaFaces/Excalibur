// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Testcontainers.CosmosDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Cosmos DB Linux-emulator container fixture for the Cosmos DB persistence-provider conformance suite.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the Cosmos event-store fixture's emulator setup (digest-pinned image, one partition, gateway
/// mode, the emulator's self-signed cert via <c>HttpClientFactory</c>). Extends
/// <see cref="ContainerFixtureBase"/>: real-infra conformance is never skipped.
/// </para>
/// <para>
/// The fixture owns a client only to create the database the provider expects to find —
/// <see cref="CosmosDbPersistenceProvider"/> takes no client and constructs its own from options, so the
/// provider's own connection is not this one.
/// </para>
/// </remarks>
public sealed class CosmosDbPersistenceProviderContainerFixture : ContainerFixtureBase
{
	private CosmosDbContainer? _container;

	/// <summary>
	/// Gets the database name the provider is pointed at; the fixture creates it.
	/// </summary>
	public string DatabaseName { get; } = "persistence_conformance";

	/// <summary>
	/// Gets the fixture-owned client, used only to provision the database.
	/// </summary>
	public CosmosClient Client { get; private set; } = null!;

	/// <summary>
	/// Gets the emulator connection string.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the emulator HttpClient, which trusts the emulator's self-signed certificate.
	/// </summary>
	public HttpClient EmulatorHttpClient => _container?.HttpClient
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(10);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new CosmosDbBuilder()
			// Pinned by digest, not tag: a tag is mutable, so a later run can silently receive a
			// different image than the one this suite's evidence was measured on.
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b")
			.WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "1")
			.WithEnvironment("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "false")
			.WithName($"cosmosdb-persistence-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		Client = new CosmosClientBuilder(_container.GetConnectionString())
			.WithConnectionModeGateway()
			.WithRequestTimeout(TimeSpan.FromSeconds(120))
			.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
			.WithHttpClientFactory(() => _container.HttpClient)
			.Build();

		_ = await Client.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			Client?.Dispose();

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
/// xUnit collection definition for the Cosmos DB persistence-provider conformance suite.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class CosmosDbPersistenceProviderTestCollection
	: ICollectionFixture<CosmosDbPersistenceProviderContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "CosmosDb Persistence Provider Integration Tests";
}
