// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using System.Text;

using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;


namespace Excalibur.EventSourcing.Tests.Projections;

internal sealed class StoreItem
{
	public string Id { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public decimal Amount { get; set; }

	public DateTimeOffset Created { get; set; }
}

/// <summary>
/// Carries a MUTABLE reference property whose text form a filter CAN match, so the capture allowlist is
/// pinned by a test rather than by agreeing with itself.
/// </summary>
/// <remarks>
/// A <see cref="StringBuilder"/> is chosen deliberately over a collection. A collection property proves
/// nothing here: capturing it by reference still fails the filter, because the comparison falls through
/// to a text compare of "System.Collections.Generic.List`1[System.String]" and never matches whatever
/// the caller asked for. So a collection-based arm asserts the right outcome for the wrong reason and
/// stays green when the allowlist is removed -- measured, not assumed. A StringBuilder's text form is
/// the caller's own string, so an aliased capture becomes VISIBLE as a match.
/// </remarks>
internal sealed class StoreItemWithNotes
{
	public string Id { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public StringBuilder Notes { get; set; } = new();
}

/// <summary>
/// The in-memory projection store, which is what makes a first projection runnable without a cluster.
/// </summary>
/// <remarks>
/// It exists because four separate trees in this repository had each hand-written one, and because the
/// documented Quick Start could not run without it. It answers the same filter grammar the remote stores
/// answer, through the same <see cref="FilterParser"/>, so a test written against it means something
/// about the store that replaces it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryProjectionStoreShould
{
	private static readonly CancellationToken None = CancellationToken.None;

	[Fact]
	public async Task RoundTripAProjection()
	{
		var store = new InMemoryProjectionStore<StoreItem>();

		await store.UpsertAsync("a", new StoreItem { Id = "a", Status = "Placed" }, None);

		(await store.GetByIdAsync("a", None))!.Status.ShouldBe("Placed");
	}

	[Fact]
	public async Task ReportAnAbsentProjectionAsNotFound() =>
		(await new InMemoryProjectionStore<StoreItem>().GetByIdAsync("missing", None)).ShouldBeNull();

	[Fact]
	public async Task ReplaceOnASecondUpsertOfTheSameId()
	{
		var store = new InMemoryProjectionStore<StoreItem>();

		await store.UpsertAsync("a", new StoreItem { Id = "a", Status = "Placed" }, None);
		await store.UpsertAsync("a", new StoreItem { Id = "a", Status = "Shipped" }, None);

		(await store.GetByIdAsync("a", None))!.Status.ShouldBe("Shipped");
		(await store.CountAsync(null, None)).ShouldBe(1);
	}

	/// <summary>
	/// The interface states deletion is idempotent, so deleting twice must not throw -- the arm exists
	/// because an implementation over a dictionary gets this wrong by returning a bool nobody checks.
	/// </summary>
	[Fact]
	public async Task DeleteIdempotently()
	{
		var store = new InMemoryProjectionStore<StoreItem>();
		await store.UpsertAsync("a", new StoreItem { Id = "a" }, None);

		await store.DeleteAsync("a", None);
		await store.DeleteAsync("a", None);

        (await store.GetByIdAsync("a", None)).ShouldBeNull();
	}

	[Fact]
	public async Task ReturnEveryProjectionWhenNoFilterIsGiven()
	{
		var store = await ThreeItems();

		(await store.QueryAsync(null, null, None)).Count.ShouldBe(3);
		(await store.CountAsync(null, None)).ShouldBe(3);
	}

	[Theory]
	[InlineData("Status", "Shipped", 1)]
	[InlineData("Status:neq", "Shipped", 2)]
	[InlineData("Amount:gt", 150, 1)]
	[InlineData("Amount:gte", 100, 3)]
	[InlineData("Amount:lt", 150, 2)]
	[InlineData("Amount:lte", 100, 1)]
	public async Task AnswerTheSharedFilterGrammar(string key, object value, int expected)
	{
		var store = await ThreeItems();

		var filters = new Dictionary<string, object> { [key] = value };

		(await store.QueryAsync(filters, null, None)).Count.ShouldBe(expected);
		(await store.CountAsync(filters, None)).ShouldBe(expected);
	}

	[Fact]
	public async Task AnswerTheInAndContainsOperators()
	{
		var store = await ThreeItems();

		var inFilter = new Dictionary<string, object> { ["Status:in"] = new[] { "Placed", "Shipped" } };
		(await store.QueryAsync(inFilter, null, None)).Count.ShouldBe(2);

		var containsFilter = new Dictionary<string, object> { ["Status:contains"] = "ipp" };
		(await store.QueryAsync(containsFilter, null, None)).Count.ShouldBe(1);
	}

	[Fact]
	public async Task PageAndOrderAsQueryOptionsAsk()
	{
		var store = await ThreeItems();

		var ordered = await store.QueryAsync(null, new QueryOptions(OrderBy: nameof(StoreItem.Amount)), None);
		ordered.Select(static i => i.Amount).ShouldBe([100m, 125m, 200m]);

		var descending = await store.QueryAsync(
			null,
			new QueryOptions(OrderBy: nameof(StoreItem.Amount), Descending: true),
			None);
		descending[0].Amount.ShouldBe(200m);

		var page = await store.QueryAsync(
			null,
			new QueryOptions(Skip: 1, Take: 1, OrderBy: nameof(StoreItem.Amount)),
			None);
		page.Count.ShouldBe(1);
		page[0].Amount.ShouldBe(125m);
	}

	/// <summary>
	/// HONESTY ARM. The interface says a store presenting no tenant-scoping capability is not confined by
	/// the framework. This store is not confined, so it must present no marker — a store that claimed one
	/// it does not honour is the lying-capability shape, and it would pass a multi-tenant host's
	/// capability requirement while isolating nothing.
	/// </summary>
	[Fact]
	public void PresentNoTenantScopingCapabilityItDoesNotHave()
	{
		IProjectionStore<StoreItem> store = new InMemoryProjectionStore<StoreItem>();

		// Through the interface: GetService is a default interface implementation, so the capability
		// question is asked the way a consumer's decorator chain asks it.
		store.GetService(typeof(ITenantScopingCapability<IProjectionStore<StoreItem>>)).ShouldBeNull();
	}

	/// <summary>
	/// The registration satisfies the start-up store requirement, which is the whole point of shipping
	/// it: the documented Quick Start registers a projection, and this is what gives it somewhere to go.
	/// </summary>
	[Fact]
	public void SatisfyTheStartupStoreRequirement()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var es = new ExcaliburEventSourcingBuilder(services);
		_ = es.AddProjection<StoreItem>(p => p.Async());
		_ = es.Services.AddInMemoryProjectionStore<StoreItem>();

		using var provider = services.BuildServiceProvider();

		foreach (var validator in provider.GetServices<IStartupPrerequisiteValidator>())
		{
			validator.Validate();
		}
	}

	/// <summary>
	/// Registered as a SINGLETON on purpose: a scoped in-memory store would discard everything it held
	/// at the end of each request, which looks exactly like a projection that never advanced.
	/// </summary>
	[Fact]
	public async Task KeepItsContentsAcrossScopes()
	{
		var services = new ServiceCollection();
		_ = services.AddInMemoryProjectionStore<StoreItem>();
		using var provider = services.BuildServiceProvider();

		using (var first = provider.CreateScope())
		{
			await first.ServiceProvider.GetRequiredService<IProjectionStore<StoreItem>>()
				.UpsertAsync("a", new StoreItem { Id = "a", Status = "Placed" }, None);
		}

		using var second = provider.CreateScope();
		var seen = await second.ServiceProvider.GetRequiredService<IProjectionStore<StoreItem>>()
			.GetByIdAsync("a", None);

		seen.ShouldNotBeNull();
	}

	/// <summary>
	/// A caller that mutates its own object AFTER the write must not change what a filtered count or
	/// query returns. Both halves are asserted: the SAFETY half (the mutation does not leak in) and the
	/// LIVENESS half (the filter still matches the value that was actually stored) -- a store that
	/// returned nothing to anybody would satisfy the first one alone.
	/// </summary>
	/// <remarks>
	/// This guards the write-time capture that lets CountAsync filter without rehydrating. A future
	/// change that captured a MUTABLE reference instead of a value would pass every existing arm and
	/// fail this one.
	/// </remarks>
	[Fact]
	public async Task NotLetACallerMutateWhatAFilteredReadReturns()
	{
		var store = new InMemoryProjectionStore<StoreItem>();
		var mine = new StoreItem { Id = "a", Status = "Paid", Amount = 100m };
		await store.UpsertAsync("a", mine, None);

		mine.Status = "Refunded";
		mine.Amount = 999m;

		var paid = new Dictionary<string, object> { ["Status"] = "Paid" };
		var refunded = new Dictionary<string, object> { ["Status"] = "Refunded" };

		// LIVENESS: the stored value still matches.
		(await store.CountAsync(paid, None)).ShouldBe(
			1,
			customMessage: "the filter must see the value as it was written, not as the caller later changed it");
		(await store.QueryAsync(paid, null, None)).Count.ShouldBe(1);

		// SAFETY: the caller's later value did not leak into the store.
		(await store.CountAsync(refunded, None)).ShouldBe(0);
		(await store.QueryAsync(refunded, null, None)).ShouldBeEmpty();
	}

	/// <summary>
	/// A mutable reference property is never captured for filtering, so a caller cannot make a row match
	/// a filter AFTER writing it by mutating the object it handed over.
	/// </summary>
	/// <remarks>
	/// This is the arm that actually binds the capture allowlist. Removing the allowlist -- so every
	/// property type is captured -- stores the caller's live StringBuilder, the post-write mutation then
	/// shows through the filter's text comparison, and the first assertion below goes RED. The LIVENESS
	/// half sits in the same arm: the row stays reachable by a filter a column could answer, so a store
	/// that simply matched nothing would not pass.
	/// </remarks>
	[Fact]
	public async Task NotLetAPostWriteMutationOfAReferencePropertyMakeARowMatch()
	{
		var store = new InMemoryProjectionStore<StoreItemWithNotes>();
		var mine = new StoreItemWithNotes { Id = "a", Status = "Paid", Notes = new StringBuilder("draft") };
		await store.UpsertAsync("a", mine, None);

		_ = mine.Notes.Clear().Append("final");

		// SAFETY: the mutation cannot reach the stored row, so nothing matches it. An aliased capture
		// would report 1 here.
		var onNotes = new Dictionary<string, object> { ["Notes"] = "final" };
		(await store.CountAsync(onNotes, None)).ShouldBe(
			0,
			customMessage: "a caller's post-write mutation must not be visible to a filter");

		// LIVENESS: the row is still there and still reachable by a column-shaped filter.
		var onStatus = new Dictionary<string, object> { ["Status"] = "Paid" };
		(await store.CountAsync(onStatus, None)).ShouldBe(1);
	}

	private static async Task<InMemoryProjectionStore<StoreItem>> ThreeItems()
	{
		var store = new InMemoryProjectionStore<StoreItem>();

		await store.UpsertAsync("a", new StoreItem { Id = "a", Status = "Placed", Amount = 100m }, None);
		await store.UpsertAsync("b", new StoreItem { Id = "b", Status = "Shipped", Amount = 200m }, None);
		await store.UpsertAsync("c", new StoreItem { Id = "c", Status = "Paid", Amount = 125m }, None);

		return store;
	}
}
