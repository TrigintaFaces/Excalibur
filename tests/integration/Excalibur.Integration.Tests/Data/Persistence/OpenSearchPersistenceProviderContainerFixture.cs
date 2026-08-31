// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// OpenSearch container fixture for the OpenSearch persistence-provider conformance suite.
/// </summary>
/// <remarks>
/// Extends <see cref="ContainerFixtureBase"/> rather than the graceful-degradation fixture used by the
/// OpenSearch projection suite: real-infra conformance is never skipped, so a missing container surfaces
/// as a failure rather than a silent absence. A skipped arm is recorded as not-executed and is excluded
/// from the executed and passed counters — but the run still exits green with zero failures, so a
/// conformance contract can go unverified without any red appearing. Only executed-against-expected
/// shows it, and nothing compares those here.
/// </remarks>
public sealed class OpenSearchPersistenceProviderContainerFixture : ContainerFixtureBase
{
	private IContainer? _container;

	/// <summary>
	/// Gets the base address of the running OpenSearch node.
	/// </summary>
	public Uri Endpoint => _container is not null
		? new Uri($"http://localhost:{_container.GetMappedPublicPort(9200)}")
		: throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ContainerBuilder()
			.WithImage("opensearchproject/opensearch:2.16.0")
			.WithName($"opensearch-persistence-{Guid.NewGuid():N}")
			.WithPortBinding(9200, true)
			.WithEnvironment("discovery.type", "single-node")
			.WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
			.WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
			.WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms256m -Xmx256m")
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilHttpRequestIsSucceeded(static r => r.ForPort(9200).ForPath("/")))
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
/// xUnit collection definition for the OpenSearch persistence-provider conformance suite.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class OpenSearchPersistenceProviderTestCollection
	: ICollectionFixture<OpenSearchPersistenceProviderContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "OpenSearch Persistence Provider Integration Tests";
}
