// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Oracle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the Oracle saga store (S876 A4). Runs the shared
/// <see cref="SagaStoreConformanceTestBase"/> contract with <see cref="SupportsOptimisticConcurrency"/>
/// enabled against a real <c>gvenzl/oracle-free</c> container — so the version-gated no-overwrite and
/// no-resurrect facts, plus save/load round-trip and completed-purge, are enforced on real Oracle.
/// </summary>
/// <remarks>
/// The store performs a store-owns-increment compare-and-swap via a version-gated Oracle <c>MERGE</c>
/// (<c>OracleSagaStore.SaveAsync</c> surfaces a 0-row merge as <see cref="ConcurrencyException"/>) and
/// scopes loads to <c>{SagaId, SagaType}</c> for type-isolation. This is a NON-SKIPPED real-infra lock:
/// Docker must be available.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Oracle")]
[Collection("Oracle SagaStore Integration Tests")]
public sealed class OracleSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase, IClassFixture<OracleSagaStoreContainerFixture>
{
	private readonly OracleSagaStoreContainerFixture _fixture;

	public OracleSagaStoreConcurrencyConformanceShould(OracleSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override bool SupportsOptimisticConcurrency => true;

	/// <inheritdoc/>
	protected override async Task<ISagaStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — this real-infra conformance lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new OracleSagaStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName,
		});

		return new OracleSagaStore(
			_fixture.ConnectionString,
			options,
			NullLogger<OracleSagaStore>.Instance,
			new DispatchJsonSerializer(),
			SingleTenantTestContext.Instance);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();

	/// <summary>
	/// A0-ruling #5 lock: Oracle folds a zero-length string to NULL. A saga saved with an empty
	/// <c>TenantId</c> must still round-trip and satisfy the version-gated OCC (the empty value is
	/// normalized to NULL at the store boundary so it can never defeat a not-null/identity assertion).
	/// </summary>
	[Fact]
	public async Task SaveAsync_EmptyTenantId_RoundTripsWithoutDefeatingConcurrency()
	{
		var saga = new TestSagaState
		{
			SagaId = Guid.NewGuid(),
			TenantId = string.Empty,
		};

		await Store.SaveAsync(saga, CancellationToken.None).ConfigureAwait(false);

		var reloaded = await Store.LoadAsync<TestSagaState>(saga.SagaId, CancellationToken.None)
			.ConfigureAwait(false);

		reloaded.ShouldNotBeNull();
		// The empty tenant folds to NULL on Oracle; the contract is "no value", i.e. null-or-empty.
		reloaded.TenantId.ShouldBeNullOrEmpty();
		// The authoritative version advanced to 1 despite the empty identity-adjacent field.
		reloaded.Version.ShouldBe(1L);
	}

	/// <summary>
	/// The store binds SagaId as RAW(16) bytes at BOTH the save (MERGE) and load (WHERE) sites, so a
	/// saved saga is found by its exact Guid — the stored RAW(16) matches the WHERE RAW(16) byte-for-byte
	/// — never collides with a different Guid, and a never-saved Guid genuinely resolves to null.
	/// RED on the pre-fix raw-Guid bind: ODP.NET has no native Guid bind type, so the raw <c>Guid</c>
	/// parameter throws before any row is written or read. NON-SKIPPED real-infra lock (safety: the
	/// distinct-Guid non-collision and the never-saved-Guid null; liveness: the saved Guid IS found).
	/// </summary>
	[Fact]
	public async Task SaveAsync_ThenLoadByExactGuid_RoundTripsRaw16_OnRealOracle()
	{
		var a = new TestSagaState { SagaId = Guid.NewGuid() };
		var b = new TestSagaState { SagaId = Guid.NewGuid() };

		await Store.SaveAsync(a, CancellationToken.None).ConfigureAwait(false);
		await Store.SaveAsync(b, CancellationToken.None).ConfigureAwait(false);

		// Liveness: loading by a's exact Guid returns a — the stored RAW(16) matched the WHERE RAW(16).
		var loadedA = await Store.LoadAsync<TestSagaState>(a.SagaId, CancellationToken.None).ConfigureAwait(false);
		loadedA.ShouldNotBeNull();
		loadedA.SagaId.ShouldBe(a.SagaId);

		// Safety: a's load does not resolve b's row — the RAW(16) WHERE filters by the exact 16 bytes.
		loadedA.SagaId.ShouldNotBe(b.SagaId);

		// Safety: a never-saved Guid resolves to null — the filter genuinely filters (not match-everything).
		var missing = await Store.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		missing.ShouldBeNull();
	}

	/// <summary>
	/// y6a3l8 — the saga-SUMMARY read path (<c>QuerySagaSummaries</c> / <see cref="ISagaStoreAdmin.GetSummaryAsync"/>)
	/// round-trips the <c>SagaId</c> as RAW(16) in BOTH directions on real Oracle, unlike <c>LoadAsync</c> which
	/// never SELECTs <c>SagaId</c> back (so this path was previously unexercised):
	/// <list type="number">
	/// <item><b>bind</b> — the by-id <c>WHERE SagaId = :SagaId</c> binds the <see cref="Guid"/> as RAW(16) bytes;
	/// a raw-<see cref="Guid"/> bind throws on ODP.NET (no native Guid bind type).</item>
	/// <item><b>read</b> — the <c>SELECT SagaId</c> returns the RAW(16) column as <c>byte[]</c>, which the row
	/// must reconstruct via <c>new Guid(bytes)</c> (Dapper cannot assign <c>byte[]</c>→<see cref="Guid"/>).</item>
	/// </list>
	/// RED on the pre-fix raw-Guid bind AND on the <c>byte[]</c>→<see cref="Guid"/> read. NON-SKIPPED real-infra
	/// lock — safety: the round-tripped id equals the original and a never-saved id resolves to null; liveness:
	/// the saved saga IS found via its exact Guid through the summary surface.
	/// </summary>
	[Fact]
	public async Task GetSummaryAsync_RoundTripsSagaIdGuid_OnRealOracle()
	{
		var admin = Store as ISagaStoreAdmin;
		admin.ShouldNotBeNull("the Oracle saga store must expose the ISagaStoreAdmin summary surface for y6a3l8");

		var saga = new TestSagaState { SagaId = Guid.NewGuid() };
		await Store.SaveAsync(saga, CancellationToken.None).ConfigureAwait(false);

		// Liveness + read/bind round-trip: the by-id summary binds the Guid as RAW(16) (WHERE) and reads the
		// RAW(16) column back as byte[] -> Guid (SELECT). The reconstructed id must equal the saved id exactly.
		var summary = await admin.GetSummaryAsync(saga.SagaId, CancellationToken.None).ConfigureAwait(false);
		summary.ShouldNotBeNull("the saved saga must be found by its exact Guid via the summary read path");
		summary.SagaId.ShouldBe(saga.SagaId,
			"the RAW(16) SagaId must round-trip byte-for-byte through the summary bind (WHERE) and read (SELECT)");

		// Safety: a never-saved Guid genuinely resolves to null — the RAW(16) filter filters, not match-everything.
		var absent = await admin.GetSummaryAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		absent.ShouldBeNull();
	}
}
