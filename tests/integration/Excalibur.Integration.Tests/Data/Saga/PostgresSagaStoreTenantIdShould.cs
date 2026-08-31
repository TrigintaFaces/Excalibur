// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Real-infrastructure lock (bead 952rbe, Postgres sibling) for saga tenant binding on the Postgres store:
/// a <c>SagaState.TenantId</c> must round-trip through both the serialized JSONB state blob AND the dedicated
/// queryable <c>tenant_id</c> row-column — matching the SqlServer store (cross-provider consistency, the
/// aiikde divergence class the relational stores must not re-open).
/// </summary>
/// <remarks>
/// <b>Non-vacuity:</b> RED on the pre-fix surface — without <c>SagaState.TenantId</c> the JSONB blob carries
/// no tenant, and without the schema/UPSERT column the row <c>SELECT</c> throws <c>column "tenant_id" does
/// not exist</c>. Asserted against a real Postgres container (a mock can't reproduce the UPSERT write /
/// column read); never skipped.
/// </remarks>
[Collection("PostgresSagaStore")]
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
public sealed class PostgresSagaStoreTenantIdShould : IAsyncLifetime
{
	private const string TenantId = "tenant-952rbe";
	private readonly PostgresSagaStoreContainerFixture _fixture;
	private ISagaStore _store = null!;
	private ISagaStore _untenantedStore = null!;

	public PostgresSagaStoreTenantIdShould(PostgresSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public async ValueTask InitializeAsync()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = _fixture.Schema,
			TableName = _fixture.TableName,
			CommandTimeoutSeconds = 30,
		});

		// The row's tenant term is resolved from the AMBIENT context alone, never from sagaState.TenantId
		// (SaveSagaRequest's documented contract: the read side is given a saga id and a scope, never a
		// state, so deriving the row key from the saga's own tenant would write it where no read looks).
		// A store on the untenanted partition therefore stamps the reserved untenanted term by design —
		// so this lock MUST supply the ambient tenant to exercise the tenanted write at all. The context is
		// required now, so the untenanted store below names that partition explicitly; it is the same term
		// an absent context used to resolve to, not a new one.
		_store = new PostgresSagaStore(
			options,
			NullLogger<PostgresSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(TenantId));

		_untenantedStore = new PostgresSagaStore(
			options,
			NullLogger<PostgresSagaStore>.Instance,
			new DispatchJsonSerializer(),
			UntenantedTestTenantContext.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task Persist_and_reload_TenantId_via_both_the_blob_and_the_queryable_row_column()
	{
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId, TenantId = TenantId };

		await _store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// Axis 1 — TenantId rides the serialized JSONB state blob.
		var loaded = await _store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull();
		loaded.TenantId.ShouldBe(TenantId, "TenantId must round-trip through the serialized SagaState JSONB blob");

		// Axis 2 — TenantId is also written to the dedicated queryable row-column (defense-in-depth).
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		var rowTenantId = await connection.QuerySingleAsync<string?>(
			$"SELECT tenant_id FROM \"{_fixture.Schema}\".\"{_fixture.TableName}\" WHERE saga_id = @id",
			new { id = sagaId }).ConfigureAwait(false);

		rowTenantId.ShouldBe(TenantId, "the Postgres saga UPSERT must populate the queryable tenant_id column");
	}

	/// <summary>
	/// Liveness counterpart: a save made with NO ambient tenant must land in the reserved untenanted
	/// partition — never be silently stamped with the tenant carried in the saga's own payload. Without
	/// this arm the lock above is passed by a store that simply copies <c>sagaState.TenantId</c> into the
	/// column, which is precisely the cross-tenant write the ambient-scope contract exists to prevent.
	/// </summary>
	[Fact]
	public async Task Stamp_the_untenanted_partition_when_no_ambient_tenant_is_present_even_if_the_state_carries_one()
	{
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId, TenantId = TenantId };

		await _untenantedStore.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		var rowTenantId = await connection.QuerySingleAsync<string?>(
			$"SELECT tenant_id FROM \"{_fixture.Schema}\".\"{_fixture.TableName}\" WHERE saga_id = @id",
			new { id = sagaId }).ConfigureAwait(false);

		rowTenantId.ShouldNotBe(
			TenantId,
			"an unscoped save must NOT adopt the payload's tenant as the row key — the row key is the ambient scope alone");
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
