// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.Data.Tests.ElasticSearch.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

namespace Excalibur.Data.Tests.OpenSearch;

internal sealed class OpenSearchProvisioningProjection
{
	public string Id { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;
}

/// <summary>
/// What the OpenSearch projection store does when provisioning its index fails.
/// </summary>
/// <remarks>
/// The store builds its own client with <c> ThrowExceptions() </c> when the consumer supplies none, so the
/// discarded create response was only observable on the supplied-client path -- which is the path a
/// consumer registering their own <see cref="IOpenSearchClient"/> takes, and the one exercised here. The
/// client is a real, unconnected <see cref="OpenSearchClient"/> against an endpoint returning a real server
/// error body, so the SDK's own response parsing is what the store sees.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class OpenSearchIndexProvisioningShould
{
	private const string Rejected =
		"""{"error":{"root_cause":[{"type":"illegal_argument_exception","reason":"unknown setting"}],"type":"illegal_argument_exception","reason":"unknown setting"},"status":400}""";

	private const string AlreadyExists =
		"""{"error":{"root_cause":[{"type":"resource_already_exists_exception","reason":"index already exists"}],"type":"resource_already_exists_exception","reason":"index already exists"},"status":400}""";

	private const string Acknowledged =
		"""{"acknowledged":true,"shards_acknowledged":true,"index":"provisioning"}""";

	[Fact]
	public async Task RaiseWhenTheIndexCannotBeCreated()
	{
		using var endpoint = new IndexCreationEndpoint(createStatus: 400, Rejected);
		var store = CreateStore(endpoint.Uri);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.CountAsync(filters: null, CancellationToken.None).ConfigureAwait(true))
			.ConfigureAwait(true);

		endpoint.Creates.ShouldBe(1, "the store must have attempted the create it then reported as failed");
	}

	/// <summary>
	/// LIVENESS: a create the server refuses because the index is already there must not stop the store.
	/// </summary>
	[Fact]
	public async Task StartWhenTheIndexAlreadyExists()
	{
		using var endpoint = new IndexCreationEndpoint(createStatus: 400, AlreadyExists);
		var store = CreateStore(endpoint.Uri);

		var count = await store.CountAsync(filters: null, CancellationToken.None).ConfigureAwait(true);

		count.ShouldBe(0);
	}

	/// <summary>
	/// CONTROL for the fixture: an accepted create reaches the same place.
	/// </summary>
	[Fact]
	public async Task StartWhenTheCreateIsAccepted()
	{
		using var endpoint = new IndexCreationEndpoint(createStatus: 200, Acknowledged);
		var store = CreateStore(endpoint.Uri);

		var count = await store.CountAsync(filters: null, CancellationToken.None).ConfigureAwait(true);

		count.ShouldBe(0);
		endpoint.Creates.ShouldBe(1);
	}

	private static OpenSearchProjectionStore<OpenSearchProvisioningProjection> CreateStore(Uri endpoint)
	{
		var options = new OpenSearchProjectionStoreOptions { NodeUri = endpoint.ToString() };

		var monitor = A.Fake<IOptionsMonitor<OpenSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);

		// Deliberately NOT ThrowExceptions(): the defect is invisible when the client throws for the store.
		var client = new OpenSearchClient(new ConnectionSettings(endpoint));

		return new OpenSearchProjectionStore<OpenSearchProvisioningProjection>(
			client,
			monitor,
			NullLogger<OpenSearchProjectionStore<OpenSearchProvisioningProjection>>.Instance);
	}
}
