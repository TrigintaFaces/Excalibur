// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Binds the rule that every <see cref="QueryOptions"/> member this store accepts is either applied or
/// refused — never accepted and dropped.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks:</b> <c>QueryAsync</c> read only <c>Take</c>. A caller passing <c>Skip</c> or
/// <c>OrderBy</c> received an unskipped, unordered result with no exception and no log. That is worse than
/// an unsupported operation, because the caller cannot tell it apart from a supported one: "ordered, and
/// this happens to be the order" and "ordering silently discarded" are the same observation, and a paging
/// loop built on an ignored <c>Skip</c> returns the same page forever instead of terminating.
/// </para>
/// <para>
/// <b>Why these options are honoured rather than refused.</b> Verified against the pinned SDK assembly
/// rather than from recall: <c>Query.Offset(int)</c>, <c>Query.OrderBy(string)</c> and
/// <c>Query.OrderByDescending(string)</c> all exist, so this store can satisfy every member natively. The
/// sibling DynamoDB store reaches the same conclusion by a different route: a Scan has no server-side
/// ordering, so that store must read every matched row before any of them can be known to be first, and
/// it says so on its own query method. Neither store orders a page it has already bounded — that would
/// sort an arbitrary subset and return a result indistinguishable from a correct one. The DIFFERENCE is
/// where the cost lands, and it is Firestore's native support that must not be assumed there.
/// </para>
/// <para>
/// <b>Why a real emulator.</b> Ordering and offset are executed by the database, not by us. A fake would
/// return whatever sequence the test author arranged and would prove only that the fake agrees with the
/// test; it could not show that the field path we order by is one the store actually indexed.
/// </para>
/// </remarks>
[Collection("Firestore Projection Filter Tests")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Data")]
[Trait("Database", "Firestore")]
public sealed class FirestoreProjectionStoreQueryOptionsShould : IAsyncLifetime
{
	private readonly FirestoreProjectionFilterFixture _fixture;
	private FirestoreProjectionStore<TestOrderProjection> _store = null!;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreProjectionStoreQueryOptionsShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Firestore emulator fixture.</param>
	public FirestoreProjectionStoreQueryOptionsShould(FirestoreProjectionFilterFixture fixture) =>
		_fixture = fixture;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		// Deliberately NOT a graceful skip. An option-dropping defect is invisible by construction, so a
		// lock that quietly passes when the emulator is missing would restore exactly the silence this
		// test exists to remove.
		_fixture.IsInitialized.ShouldBeTrue(
			"the Firestore emulator must be available for this lock — ordering and offset are executed by the database, so there is no faithful substitute");

		_store = new FirestoreProjectionStore<TestOrderProjection>(
			_fixture.Db,
			MsOptions.Create(new FirestoreProjectionStoreOptions { CollectionName = $"opts-{Guid.NewGuid():N}" }),
			NullLogger<FirestoreProjectionStore<TestOrderProjection>>.Instance);

		// The document ids are chosen so that DOCUMENT-ID order and QUANTITY order DISAGREE.
		//
		// This is load-bearing and the first version of this test got it wrong. Firestore's implicit order
		// is by document id, so seeding a=10, b=20, c=30 makes the unordered result already ascending by
		// quantity — and the ascending arm then passes against a store that ignores OrderBy entirely. The
		// mutation run is what exposed it: that arm stayed green while the option was being dropped.
		//
		//   document-id order : a=30, b=10, c=20   ->  [30, 10, 20]
		//   quantity ascending:                        [10, 20, 30]
		//
		// Every arm below now distinguishes "ordered" from "happened to come back that way".
		await _store.UpsertAsync("a", TestOrderProjection.Create("a", "c", "Active", quantity: 30), CancellationToken.None);
		await _store.UpsertAsync("b", TestOrderProjection.Create("b", "c", "Active", quantity: 10), CancellationToken.None);
		await _store.UpsertAsync("c", TestOrderProjection.Create("c", "c", "Active", quantity: 20), CancellationToken.None);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// SAFETY — OrderBy is applied by the database. RED against the silent drop, which returned the
	/// documents in whatever order the collection scan produced.
	/// </summary>
	[Fact]
	public async Task OrderAscendingByTheRequestedProperty()
	{
		var results = await _store.QueryAsync(
			filters: null,
			new QueryOptions(OrderBy: nameof(TestOrderProjection.Quantity)),
			CancellationToken.None);

		results.Select(r => r.Quantity).ShouldBe([10, 20, 30]);
	}

	/// <summary>
	/// SAFETY — Descending reverses the order. Without this arm an implementation that accepted OrderBy
	/// and ignored Descending would still pass the ascending arm.
	/// </summary>
	[Fact]
	public async Task OrderDescendingWhenDescendingIsRequested()
	{
		var results = await _store.QueryAsync(
			filters: null,
			new QueryOptions(OrderBy: nameof(TestOrderProjection.Quantity), Descending: true),
			CancellationToken.None);

		results.Select(r => r.Quantity).ShouldBe([30, 20, 10]);
	}

	/// <summary>
	/// SAFETY — Skip offsets into the ordered result. RED against the silent drop, which returned the
	/// first page regardless of the offset requested.
	/// </summary>
	[Fact]
	public async Task SkipTheRequestedNumberOfDocuments()
	{
		var results = await _store.QueryAsync(
			filters: null,
			new QueryOptions(Skip: 1, OrderBy: nameof(TestOrderProjection.Quantity)),
			CancellationToken.None);

		// The first document is genuinely absent, not merely reordered.
		results.Select(r => r.Quantity).ShouldBe([20, 30]);
	}

	/// <summary>
	/// SAFETY — Skip and Take compose into a stable page. This is the shape a paging caller writes, and
	/// the shape an ignored Skip breaks silently.
	/// </summary>
	[Fact]
	public async Task ComposeSkipAndTakeIntoAStablePage()
	{
		var results = await _store.QueryAsync(
			filters: null,
			new QueryOptions(Skip: 1, Take: 1, OrderBy: nameof(TestOrderProjection.Quantity)),
			CancellationToken.None);

		results.Select(r => r.Quantity).ShouldBe([20]);
	}

	/// <summary>
	/// LIVENESS — a query with no options still returns every document. Without this arm, a store that
	/// refused or dropped everything would satisfy the ordering arms vacuously.
	/// </summary>
	[Fact]
	public async Task ReturnEveryDocumentWhenNoOptionsAreSupplied()
	{
		var results = await _store.QueryAsync(filters: null, options: null, CancellationToken.None);

		results.Count.ShouldBe(3);
	}

	/// <summary>
	/// SAFETY — an ordering key this store cannot translate is refused, not dropped. The store indexes
	/// top-level scalars only, so a nested path would otherwise order by nothing at all.
	/// </summary>
	[Fact]
	public async Task RefuseAnOrderingKeyItCannotTranslate()
	{
		_ = await Should.ThrowAsync<NotSupportedException>(() => _store.QueryAsync(
			filters: null,
			new QueryOptions(OrderBy: "Customer.Name"),
			CancellationToken.None));
	}
}
