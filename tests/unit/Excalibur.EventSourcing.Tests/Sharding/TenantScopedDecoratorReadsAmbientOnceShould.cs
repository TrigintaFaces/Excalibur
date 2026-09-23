// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Sharding;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Each tenant-scoping decorator reads the ambient tenant <b>exactly once</b> per operation, so the term
/// its gate admitted is the only term it ever observed.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this proves, and what it does not.</b> A decorator that reads the ambient context twice — once
/// to gate and once for anything else — can admit an operation on one term and act on another if the
/// context's value is not stable across the two reads. These arms bind the decorator to a single read, so
/// that divergence cannot originate here. They say nothing about the store being decorated: the inner
/// store resolves its own row predicate from the same ambient source, which is a second read this
/// decorator cannot remove without a tenant parameter on the store contract. The stability of the ambient
/// value across those reads is an obligation of <c>ITenantContext</c>, whose contract states it as a
/// history constraint, and it is enforced there rather than here.
/// </para>
/// <para>
/// <b>Why the fake changes its answer every read.</b> A context returning a constant cannot distinguish
/// one read from five — the arm would pass against a decorator that read the ambient in a loop. Returning
/// a fresh term per read makes the read count observable, and makes the value the gate acted on
/// identifiable.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantScopedDecoratorReadsAmbientOnceShould
{
	private const string OneReadReason =
		"the decorator must resolve the ambient tenant exactly once per operation; a second read can "
		+ "observe a different term than the one its gate admitted";

	[Fact]
	public async Task ReadTheAmbientTenantOnce_OnTheSagaStore()
	{
		var context = new CountingTenantContext();
		var store = new TenantScopedSagaStore(A.Fake<ISagaStore>(), context);

		_ = await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);

		context.Reads.ShouldBe(1, OneReadReason);
	}

	[Fact]
	public async Task ReadTheAmbientTenantOnce_OnTheEventStore()
	{
		var context = new CountingTenantContext();
		var store = new TenantScopedEventStore(A.Fake<IEventStore>(), context);

		_ = await store.LoadAsync("agg-1", "Agg", CancellationToken.None);

		context.Reads.ShouldBe(1, OneReadReason);
	}

	[Fact]
	public async Task ReadTheAmbientTenantOnce_OnTheProjectionStore()
	{
		var context = new CountingTenantContext();
		var store = new TenantScopedProjectionStore<TenantScopedDecoratorTestProjection>(
			A.Fake<IProjectionStore<TenantScopedDecoratorTestProjection>>(),
			context);

		_ = await store.GetByIdAsync("id-1", CancellationToken.None);

		context.Reads.ShouldBe(1, OneReadReason);
	}

	[Fact]
	public async Task ReadTheAmbientTenantOncePerOperation_NotOncePerInstance()
	{
		// A decorator that cached the term in a field would read once across many operations and pass an
		// arm that counted only the first. The tenant is a property of the operation, not of the
		// instance -- a singleton decorator serving every request must re-resolve it each time.
		var context = new CountingTenantContext();
		var store = new TenantScopedSagaStore(A.Fake<ISagaStore>(), context);

		_ = await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);
		_ = await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);
		_ = await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);

		context.Reads.ShouldBe(3, "three operations must resolve the ambient tenant three times");
	}

	[Fact]
	public async Task ActOnTheFirstTermOnly_WhenTheAmbientValueChangesUnderIt()
	{
		// The substitution scenario, reduced to the half the decorator can actually guarantee: a context
		// whose value changes on every read is admitted on term 1, and the decorator never observes term 2.
		var context = new CountingTenantContext();
		var inner = A.Fake<ISagaStore>();
		var store = new TenantScopedSagaStore(inner, context);

		_ = await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);

		context.Observed.ShouldBe(["tenant-1"], OneReadReason);
		A.CallTo(() => inner.LoadAsync<SagaState>(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// An <see cref="ITenantContext"/> that deliberately violates the stability its contract requires:
	/// every read returns a fresh term, and every read is counted.
	/// </summary>
	private sealed class CountingTenantContext : ITenantContext
	{
		private readonly List<string> _observed = [];

		public int Reads => _observed.Count;

		public IReadOnlyList<string> Observed => _observed;

		public string? TenantId
		{
			get
			{
				var term = $"tenant-{_observed.Count + 1}";
				_observed.Add(term);
				return term;
			}
		}

		public bool HasTenant => true;
	}
}
