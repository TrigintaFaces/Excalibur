// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CS8618 // Non-nullable field set in InitializeAsync()

using Excalibur.Data.Firestore.Projections;
using Excalibur.EventSourcing;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.Firestore;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Author≠impl regression lock for bd-eyg3je (Sprint 848 Lane P1, MS-1) — Firestore half.
/// </summary>
/// <remarks>
/// <para>
/// Pre-fix behavior: <see cref="FirestoreProjectionStore{TProjection}"/> <c>QueryAsync</c> and
/// <c>CountAsync</c> SILENTLY IGNORE the <c>filters</c> argument — they issue an unfiltered
/// collection read (no Firestore <c>Where*</c> clause) and return/count ALL projections of type
/// T regardless of filter. That is a silent correctness failure (FR-P1.4/AC-P1.3/AC-P1.5).
/// </para>
/// <para>
/// These tests assert the POST-FIX contract behaviorally against a real Firestore emulator:
/// <list type="bullet">
/// <item>AC-P1.3 — <c>QueryAsync({Status:"Active"})</c> returns ONLY the matching documents (a
/// <c>Where</c> clause is applied, not a post-read <c>Take</c>).</item>
/// <item>AC-P1.2 — <c>CountAsync({Status:"Active"})</c> equals the matching count, not the total.</item>
/// <item>EC-P1.1 — multiple filter keys AND-combine.</item>
/// <item>EC-P1.4 — a valid filter with no matches returns empty, not all.</item>
/// <item>AC-P1.4 — null filter is unchanged (all of type T).</item>
/// </list>
/// </para>
/// <para>
/// Seam rationale: <see cref="FirestoreDb"/> is sealed with no fakeable constructor, and the
/// repo's <c>NoConcreteSdkFakesGovernance</c> rule forbids faking <c>Google.Cloud.Firestore</c>
/// SDK types. The store also exposes no abstraction over the issued <see cref="Query"/>. The only
/// faithful, non-vacuous seam is a real Firestore emulator (Testcontainers), which is why this
/// lock lives in the integration project (the unit project has neither Docker/Testcontainers nor
/// a way to construct the store). It is RED on current source (returns 3 instead of 2) and GREEN
/// after Backend's fix in <c>FirestoreProjectionStore.cs</c> (coupled impl for bd-eyg3je).
/// </para>
/// </remarks>
[Collection("Firestore Projection Filter Tests")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Data")]
[Trait("Database", "Firestore")]
public sealed class FirestoreProjectionStoreFilterShould : IClassFixture<FirestoreProjectionFilterFixture>, IAsyncLifetime
{
	private readonly FirestoreProjectionFilterFixture _fixture;
	private FirestoreProjectionStore<TestOrderProjection> _store;

	public FirestoreProjectionStoreFilterShould(FirestoreProjectionFilterFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync()
	{
		if (!_fixture.IsInitialized)
		{
			return ValueTask.CompletedTask;
		}

		_store = new FirestoreProjectionStore<TestOrderProjection>(
			_fixture.Db,
			MsOptions.Create(new FirestoreProjectionStoreOptions { CollectionName = $"proj-{Guid.NewGuid():N}" }),
			NullLogger<FirestoreProjectionStore<TestOrderProjection>>.Instance);

		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private async Task SeedAsync()
	{
		// 2 Active, 1 Pending — a filter on Status="Active" must yield exactly 2.
		await _store.UpsertAsync("1", TestOrderProjection.Create("1", "c1", "Active", quantity: 5), CancellationToken.None);
		await _store.UpsertAsync("2", TestOrderProjection.Create("2", "c2", "Pending", quantity: 5), CancellationToken.None);
		await _store.UpsertAsync("3", TestOrderProjection.Create("3", "c3", "Active", quantity: 9), CancellationToken.None);
	}

	// ── AC-P1.3 / FR-P1.4 ── filter applied as a Where clause, only matches returned ──
	[Fact]
	public async Task QueryAsync_FiltersByEquality_ReturnsOnlyMatching()
	{
		if (!_fixture.IsInitialized)
		{
			return; // Docker/emulator unavailable — skip gracefully (matches repo convention).
		}

		await SeedAsync();
		var filters = new Dictionary<string, object> { ["Status"] = "Active" };

		var results = await _store.QueryAsync(filters, options: null, CancellationToken.None);

		// Pre-fix: returns all 3 (filter ignored) → RED.
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Status == "Active");
	}

	// ── AC-P1.2 ── filtered count, not total ──
	[Fact]
	public async Task CountAsync_FiltersByEquality_CountsOnlyMatching()
	{
		if (!_fixture.IsInitialized)
		{
			return;
		}

		await SeedAsync();
		var filters = new Dictionary<string, object> { ["Status"] = "Active" };

		var count = await _store.CountAsync(filters, CancellationToken.None);

		count.ShouldBe(2); // Pre-fix: 3 → RED.
	}

	// ── EC-P1.1 ── multiple filter keys AND-combined ──
	[Fact]
	public async Task QueryAsync_WithMultipleFilters_AndCombinesAllKeys()
	{
		if (!_fixture.IsInitialized)
		{
			return;
		}

		await SeedAsync();
		// Only doc "1" is both Active AND quantity==5.
		var filters = new Dictionary<string, object> { ["Status"] = "Active", ["Quantity"] = 5 };

		var results = await _store.QueryAsync(filters, options: null, CancellationToken.None);

		results.Count.ShouldBe(1);
		results[0].Id.ShouldBe("1");
	}

	// ── EC-P1.4 ── valid filter, no matches → empty not all ──
	[Fact]
	public async Task QueryAsync_WithNoMatches_ReturnsEmpty()
	{
		if (!_fixture.IsInitialized)
		{
			return;
		}

		await SeedAsync();
		var filters = new Dictionary<string, object> { ["Status"] = "Cancelled" };

		var results = await _store.QueryAsync(filters, options: null, CancellationToken.None);

		results.ShouldBeEmpty(); // Pre-fix: returns all 3 → RED.
	}

	// ── AC-P1.4 ── null filter unchanged (all of type T) ──
	[Fact]
	public async Task QueryAsync_WithNullFilter_ReturnsAll()
	{
		if (!_fixture.IsInitialized)
		{
			return;
		}

		await SeedAsync();

		var results = await _store.QueryAsync(filters: null, options: null, CancellationToken.None);

		results.Count.ShouldBe(3);
	}

	// ── EC-P1.5 ── round-trip fidelity guardrail (SA ruling msg 15200/15204): a projection with a
	// `decimal` + `DateTimeOffset` is written then read back EXACTLY equal. The canonical ["data"] JSON
	// blob is the deserialization source of truth; the denormalized flat query fields are write-only
	// index duplicates (Firestore-native double/Timestamp) and must NEVER be read back into the
	// projection. RED if deserialization ever sources the lossy flat fields instead of the blob.
	[Fact]
	public async Task QueryAsync_PreservesDecimalAndDateTimeOffsetFidelity_FromCanonicalBlob()
	{
		if (!_fixture.IsInitialized)
		{
			return;
		}

		// More significant digits than a double can hold + a sub-second, non-UTC DateTimeOffset — both
		// lose precision through Firestore-native field representations (decimal→double, →Timestamp/UTC).
		const decimal amount = 1234567890.123456789m;
		var createdAt = new DateTimeOffset(2026, 6, 25, 13, 45, 12, TimeSpan.FromHours(5)).AddTicks(6789);

		await _store.UpsertAsync(
			"fidelity-1",
			TestOrderProjection.Create("fidelity-1", "c9", "Active", amount: amount, quantity: 7, createdAt: createdAt),
			CancellationToken.None);

		var all = await _store.QueryAsync(filters: null, options: null, CancellationToken.None);
		var read = all.Single(p => p.Id == "fidelity-1");

		read.Amount.ShouldBe(amount, "decimal must round-trip EXACTLY from the canonical blob (no decimal→double loss)");
		read.CreatedAt.ShouldBe(createdAt, "DateTimeOffset must round-trip EXACTLY from the canonical blob (no Timestamp/offset loss)");
	}

	// ── FR-P1.5 / AC-P1.5 ── an untranslatable (nested, non-top-level-scalar) filter key MUST throw
	// NotSupportedException — never a silent unfiltered return. RED on current source (filters ignored,
	// nothing thrown); GREEN once the flat-field fix translates top-level scalars and rejects the rest.
	[Fact]
	public async Task QueryAsync_WithUntranslatableNestedFilterKey_ThrowsNotSupported()
	{
		if (!_fixture.IsInitialized)
		{
			return;
		}

		await SeedAsync();
		var nestedKey = new Dictionary<string, object> { ["Customer.Region"] = "us" };

		_ = await Should.ThrowAsync<NotSupportedException>(async () =>
			await _store.QueryAsync(nestedKey, options: null, CancellationToken.None));
	}
}

/// <summary>
/// Firestore-emulator fixture for <see cref="FirestoreProjectionStoreFilterShould"/>. Starts a
/// Firestore emulator container and builds a <see cref="FirestoreDb"/> bound to it. Mirrors the
/// established emulator-wiring pattern in <c>FirestoreEventStoreTelemetryTestFixture</c>.
/// </summary>
public sealed class FirestoreProjectionFilterFixture : IAsyncLifetime
{
	private readonly FirestoreContainer _container;

	public FirestoreProjectionFilterFixture()
	{
		_container = new FirestoreBuilder()
			.WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
			.WithName($"firestore-projfilter-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();
	}

	/// <summary>Gets a value indicating whether the emulator started and the DB is usable.</summary>
	public bool IsInitialized { get; private set; }

	/// <summary>Gets the emulator-bound Firestore database.</summary>
	public FirestoreDb Db { get; private set; } = null!;

	public string ProjectId { get; } = "test-project";

	public async ValueTask InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);

			var builder = new FirestoreDbBuilder
			{
				ProjectId = ProjectId,
				Endpoint = _container.GetEmulatorEndpoint(),
				ChannelCredentials = ChannelCredentials.Insecure,
			};
			Db = await builder.BuildAsync().ConfigureAwait(false);

			IsInitialized = true;
		}
		catch (Exception)
		{
			// Docker unavailable (e.g., CI without Docker) — tests skip gracefully.
			IsInitialized = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			var disposeTask = _container.DisposeAsync().AsTask();
			var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
			if (completed == disposeTask)
			{
				await disposeTask.ConfigureAwait(false);
			}
		}
		catch
		{
			// Best effort.
		}
	}
}

/// <summary>
/// Collection definition ensuring the Firestore projection-filter tests run sequentially
/// (one emulator container, no cross-test contention).
/// </summary>
[CollectionDefinition("Firestore Projection Filter Tests")]
public sealed class FirestoreProjectionFilterTestCollection : ICollectionFixture<FirestoreProjectionFilterFixture>;
