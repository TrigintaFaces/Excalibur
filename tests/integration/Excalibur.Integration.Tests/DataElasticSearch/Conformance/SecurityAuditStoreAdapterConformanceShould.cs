// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Security;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="SecurityAuditStoreAdapter"/>.
/// Verifies the S799-A seam 1/6 (<c>9f55a3956</c>) faithfully passes through
/// to the underlying <see cref="ElasticsearchClient"/> — exercising
/// <see cref="ISecurityAuditStore.EnsureAuditIndexTemplateAsync"/> (both
/// create and idempotent paths) and
/// <see cref="ISecurityAuditStore.BulkAppendEventsAsync"/> against a real
/// Elasticsearch TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract: one real-SDK smoke per adapter
/// under the integration shard. Behaviorally-exhaustive security-audit
/// tests already live in <see cref="SecurityAuditorShould"/> unit suites;
/// this smoke is strictly the seam-passthrough assertion, not a re-test of
/// Elasticsearch behavior. Per OVERWATCH msg 1807 (S799 A-stream scope lock).
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Feature", "Security")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class SecurityAuditStoreAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly SecurityAuditStoreAdapter _adapter;
	private bool _disposed;

	public SecurityAuditStoreAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_adapter = new SecurityAuditStoreAdapter(_client);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new SecurityAuditStoreAdapter(null!));

	[Fact]
	public async Task EnsureAuditIndexTemplateAsync_IsIdempotentAgainstRealClient()
	{
		// Act 1 — first call against a real ES cluster. The adapter
		// checks existence via _inner.Indices.ExistsIndexTemplateAsync, then
		// provisions via _inner.Indices.PutIndexTemplateAsync when absent.
		// The return value is true only when we created the template on this
		// call; on a shared-collection run the template may already exist.
		var first = await _adapter
			.EnsureAuditIndexTemplateAsync(CancellationToken.None)
			.ConfigureAwait(false);

		// Act 2 — second call: adapter must short-circuit via the Exists
		// check and return false. This is the ADR-142 §D7 idempotency
		// contract crossing the real SDK surface.
		var second = await _adapter
			.EnsureAuditIndexTemplateAsync(CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — second call is always false (template exists by now,
		// whether this test or a prior test created it).
		second.ShouldBeFalse();

		// And if we were the creator, the first call observed that.
		// (first may be true or false depending on collection ordering —
		// both outcomes are valid idempotency states.)
		_ = first;
	}

	[Fact]
	public async Task BulkAppendEventsAsync_PassesThroughToRealClient()
	{
		// Arrange — ensure the audit template exists so the target index
		// is materialised with the correct mapping.
		_ = await _adapter
			.EnsureAuditIndexTemplateAsync(CancellationToken.None)
			.ConfigureAwait(false);

		var events = new List<SecurityAuditEvent>
		{
			new()
			{
				EventId = Guid.NewGuid().ToString(),
				Timestamp = DateTimeOffset.UtcNow,
				EventType = SecurityAuditEventType.Authentication,
				Severity = SecurityEventSeverity.Low,
				Source = "conformance-smoke",
				UserId = "smoke-user",
			},
		};

		// Act — adapter constructs a BulkRequest + BulkIndexOperation<T>
		// internally and dispatches through _inner.BulkAsync.
		var result = await _adapter
			.BulkAppendEventsAsync(events, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — real ES accepts the batch; passthrough reports success
		// and the submitted count.
		result.Success.ShouldBeTrue(result.ErrorDetails);
		result.AppendedCount.ShouldBe(1);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		// ElasticsearchClient does not implement IDisposable in v8; the
		// underlying transport is managed by the client's lifetime — nothing
		// to dispose here. Method retained for xUnit IDisposable contract.
	}
}
