// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Excalibur.Data.OpenSearch.MaterializedViews;
using Excalibur.EventSourcing;
using Excalibur.Integration.Tests.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.MaterializedViews;

/// <summary>
/// OpenSearch container fixture for the materialized-view position checkpoint.
/// </summary>
/// <remarks>
/// The client is built with the SDK's DEFAULT field-name inference. That is load-bearing rather than
/// convenient: the position advance is a Painless script addressing fields by their SERIALIZED names, and
/// the default inference camelCases them. A fixture configuring a different naming policy would exercise
/// a shape no consumer runs — and a probe that wrote its own PascalCase documents is precisely how an
/// earlier draft of that script passed while being wrong.
/// </remarks>
public sealed class OpenSearchViewPositionContainerFixture : ContainerFixtureBase
{
	private IContainer? _container;

	/// <summary>Gets the OpenSearch client, built with default settings.</summary>
	public OpenSearchClient Client { get; private set; } = null!;

	/// <summary>Gets the unique views index name for this fixture.</summary>
	public string ViewsIndexName { get; } = $"views-test-{Guid.NewGuid():N}";

	/// <summary>Gets the unique positions index name for this fixture.</summary>
	public string PositionsIndexName { get; } = $"positions-test-{Guid.NewGuid():N}";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <summary>
	/// Drops the positions index so each arm starts with no checkpoint at all.
	/// </summary>
	/// <remarks>
	/// Drops the INDEX rather than one document: deleting a single document would couple this fixture to
	/// the store's private tenant-qualified identifier scheme, and calling the store's own reset would let
	/// a defect in the code under test become this suite's setup.
	/// </remarks>
	public async Task DropPositionsIndexAsync()
	{
		_ = await Client.Indices.DeleteAsync(PositionsIndexName).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ContainerBuilder()
			.WithImage("opensearchproject/opensearch:2.16.0")
			.WithName($"opensearch-viewpos-{Guid.NewGuid():N}")
			.WithPortBinding(9200, true)
			.WithEnvironment("discovery.type", "single-node")
			.WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
			.WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
			.WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilHttpRequestIsSucceeded(static r => r.ForPort(9200).ForPath("/")))
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var uri = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(9200)}");
		Client = new OpenSearchClient(new ConnectionSettings(uri));
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
			// Suppress disposal errors and timeouts to prevent a test-host crash.
		}
	}
}

/// <summary>
/// Runs the view-position checkpoint contract against REAL OpenSearch.
/// </summary>
/// <remarks>
/// <b>This suite exists because its absence was the defect.</b> The checkpoint repair was verified on
/// Elasticsearch and not here, and the arm that was skipped —
/// <see cref="ViewPositionCheckpointConformance.AcceptALowAdvanceImmediatelyAfterAReset"/> — is the one
/// that failed. The arms are shared with the Elasticsearch suite so that asymmetry cannot recur silently.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "MaterializedViews")]
public sealed class OpenSearchViewPositionShould(OpenSearchViewPositionContainerFixture fixture)
	: ViewPositionCheckpointConformance, IClassFixture<OpenSearchViewPositionContainerFixture>
{
	/// <inheritdoc/>
	protected override async Task<IMaterializedViewStore> NewStoreAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			"this suite measures the ENGINE's behaviour, so it is never skipped: a skip here is how the "
			+ "defect it binds reached consumers");

		await fixture.DropPositionsIndexAsync().ConfigureAwait(false);

		var options = Options.Create(new OpenSearchMaterializedViewStoreOptions
		{
			ViewsIndexName = fixture.ViewsIndexName,
			PositionsIndexName = fixture.PositionsIndexName,
			RefreshPolicy = "true",
		});

		return new OpenSearchMaterializedViewStore(
			fixture.Client,
			options,
			NullLogger<OpenSearchMaterializedViewStore>.Instance,
			UntenantedTestTenantContext.Instance);
	}
}
