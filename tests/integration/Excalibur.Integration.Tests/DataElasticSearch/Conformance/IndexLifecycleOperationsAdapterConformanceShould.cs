// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="IndexLifecycleOperationsAdapter"/>.
/// Verifies the S800-A seam 3/6 (<c>d16a08e84</c>) faithfully passes through
/// to the underlying <see cref="ElasticsearchClient"/> — exercising
/// <see cref="IIndexLifecycleOperations.PutPolicyAsync"/> +
/// <see cref="IIndexLifecycleOperations.DeletePolicyAsync"/> (both Deleted
/// and NotFound outcomes) +
/// <see cref="IIndexLifecycleOperations.GetStatusAsync"/> against a real
/// Elasticsearch TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract: one real-SDK smoke per adapter
/// under the integration shard. Behaviorally-exhaustive ILM tests
/// live elsewhere in the integration suite; this smoke is strictly the
/// seam-passthrough assertion. Per COMPASS S800 msg 1853 binding guidance
/// and OVERWATCH task #535 dispatch.
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class IndexLifecycleOperationsAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly IndexLifecycleOperationsAdapter _adapter;
	private bool _disposed;

	public IndexLifecycleOperationsAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_adapter = new IndexLifecycleOperationsAdapter(_client);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new IndexLifecycleOperationsAdapter(null!));

	[Fact]
	public async Task PutPolicyAsync_DeletePolicyAsync_RoundTripsAgainstRealClient()
	{
		// Arrange
		var policyName = $"conformance-ilm-{Guid.NewGuid():N}";
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration
			{
				MinAge = TimeSpan.Zero,
				Priority = 100,
				Rollover = new RolloverConditions
				{
					MaxAge = TimeSpan.FromDays(7),
					MaxDocs = 1_000_000,
					MaxSize = "50gb",
				},
			},
		};

		try
		{
			// Act 1 — adapter routes to _inner.IndexLifecycleManagement.PutLifecycleAsync.
			var put = await _adapter
				.PutPolicyAsync(policyName, policy, CancellationToken.None)
				.ConfigureAwait(false);
			put.Success.ShouldBeTrue(put.ErrorDetails);
		}
		finally
		{
			// Act 2 — adapter routes to _inner.IndexLifecycleManagement.DeleteLifecycleAsync.
			// Outcome is Deleted when the policy existed (positive path here).
			var delete = await _adapter
				.DeletePolicyAsync(policyName, CancellationToken.None)
				.ConfigureAwait(false);
			delete.ShouldBe(LifecyclePolicyDeleteOutcome.Deleted);
		}
	}

	[Fact]
	public async Task DeletePolicyAsync_NotFound_ReturnsNotFoundOrDeletedOutcome()
	{
		// Arrange — name that definitely does not exist in the cluster.
		var policyName = $"conformance-ilm-nonexistent-{Guid.NewGuid():N}";

		// Act — adapter translates the response into a domain-shaped enum.
		// ES 8 returned 404 → NotFound. ES 9 treats the operation as
		// idempotent and returns success → Deleted. Both are valid
		// seam-passthrough outcomes (ADR-142 §D7).
		var outcome = await _adapter
			.DeletePolicyAsync(policyName, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — accept either NotFound (ES 8) or Deleted (ES 9 idempotent).
		outcome.ShouldBeOneOf(
			LifecyclePolicyDeleteOutcome.NotFound,
			LifecyclePolicyDeleteOutcome.Deleted);
	}

	[Fact]
	public async Task GetStatusAsync_ReturnsEmptyListForNonMatchingPattern()
	{
		// Arrange — pattern guaranteed not to match any index.
		var indexPattern = $"conformance-ilm-empty-{Guid.NewGuid():N}-*";

		// Act — adapter routes to _inner.IndexLifecycleManagement.ExplainLifecycleAsync
		// (or the raw transport /_ilm/explain endpoint) and translates the
		// response into a domain-shaped IReadOnlyList<IndexLifecycleStatus>.
		var status = await _adapter
			.GetStatusAsync(indexPattern, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — must not throw on no-match; empty list is the contract.
		status.ShouldNotBeNull();
		status.ShouldBeEmpty();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		// ElasticsearchClient does not implement IDisposable in v8; the
		// underlying transport is managed by the client's lifetime. Method
		// retained for xUnit IDisposable contract.
	}
}
