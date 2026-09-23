// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Sharding;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Binds all three tenant-scoping decorators to one partition predicate: the same ambient value must be
/// admitted, or refused, by the decorator and by the scope resolution inside the store it wraps.
/// </summary>
/// <remarks>
/// <para>
/// A whitespace tenant is refused by <c>TenantScope.Scoped</c>, which every first-party store resolves
/// through. A decorator testing only for null or empty lets that value past its own check and the
/// operation fails one layer down instead — same exception type, different provenance, and a consumer
/// store that does not resolve a scope would not fail at all. That divergence was real, and was
/// resynchronised by hand across three separate copies of the predicate; these arms exist so the next
/// copy cannot be introduced silently.
/// </para>
/// <para>
/// <b>All three decorators, not one.</b> The predicate was previously locked only on the saga store, so
/// the projection store carried no whitespace arm at all and could have drifted alone without a single
/// test going red.
/// </para>
/// <para>
/// <b>Three outcomes, deliberately.</b> Refusal alone is satisfied by a decorator that rejects
/// everything, so a real tenant must still pass. And the explicitly-untenanted context must pass too:
/// the untenanted sentinel names a live partition on a multi-tenant deployment, so a gate tightened to
/// demand a <em>real</em> tenant would refuse a caller that correctly said "no tenant" — turning a
/// supported deployment into a hard failure on every operation.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TenantScopedDecoratorPredicateShould
{
	private const string NotARealTermReason =
		"the decorator must refuse the same values the store's own scope resolution refuses, or the value "
		+ "passes this check and fails inside the store instead";

	// ---------- SAFETY: a term that names no partition is refused by every decorator ----------

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task RefuseAnAmbientTenantThatIsNotARealTerm(string? ambient)
	{
		var store = new TenantScopedSagaStore(A.Fake<ISagaStore>(), new FixedTenantContext(ambient));

		_ = await Should.ThrowAsync<TenantRequiredException>(
			async () => await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None),
			NotARealTermReason);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task RefuseAnAmbientTenantThatIsNotARealTerm_OnTheEventStore(string? ambient)
	{
		var store = new TenantScopedEventStore(A.Fake<IEventStore>(), new FixedTenantContext(ambient));

		_ = await Should.ThrowAsync<TenantRequiredException>(
			async () => await store.LoadAsync("agg-1", "Agg", CancellationToken.None),
			NotARealTermReason);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task RefuseAnAmbientTenantThatIsNotARealTerm_OnTheProjectionStore(string? ambient)
	{
		// The arm the predicate lock was missing: the projection decorator had no whitespace coverage at
		// all, so it alone could carry a narrower predicate than the store it wraps and stay green.
		var store = new TenantScopedProjectionStore<TenantScopedDecoratorTestProjection>(
			A.Fake<IProjectionStore<TenantScopedDecoratorTestProjection>>(),
			new FixedTenantContext(ambient));

		_ = await Should.ThrowAsync<TenantRequiredException>(
			async () => await store.GetByIdAsync("id-1", CancellationToken.None),
			NotARealTermReason);
	}

	// ---------- LIVENESS: a real tenant still reaches the inner store ----------

	[Fact]
	public async Task PassARealTenantThrough()
	{
		var inner = A.Fake<ISagaStore>();
		var store = new TenantScopedSagaStore(inner, new FixedTenantContext("tenant-a"));

		_ = await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);

		A.CallTo(() => inner.LoadAsync<SagaState>(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassARealTenantThrough_OnTheEventStore()
	{
		var inner = A.Fake<IEventStore>();
		var store = new TenantScopedEventStore(inner, new FixedTenantContext("tenant-a"));

		_ = await store.LoadAsync("agg-1", "Agg", CancellationToken.None);

		A.CallTo(() => inner.LoadAsync("agg-1", "Agg", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassARealTenantThrough_OnTheProjectionStore()
	{
		var inner = A.Fake<IProjectionStore<TenantScopedDecoratorTestProjection>>();
		var store = new TenantScopedProjectionStore<TenantScopedDecoratorTestProjection>(inner, new FixedTenantContext("tenant-a"));

		_ = await store.GetByIdAsync("id-1", CancellationToken.None);

		A.CallTo(() => inner.GetByIdAsync("id-1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	// ---------- LIVENESS: the explicitly-untenanted partition is a partition, and must be admitted ----------

	[Fact]
	public async Task AdmitTheExplicitlyUntenantedPartition_OnEveryDecorator()
	{
		// UntenantedContext resolves the reserved sentinel and is documented for deployments that have
		// enabled multi-tenancy — exactly where these decorators are registered. A gate narrowed to
		// "a REAL tenant" would refuse it, so this arm is what stops that narrowing.
		var sagaInner = A.Fake<ISagaStore>();
		var eventInner = A.Fake<IEventStore>();
		var projectionInner = A.Fake<IProjectionStore<TenantScopedDecoratorTestProjection>>();

		_ = await new TenantScopedSagaStore(sagaInner, UntenantedContext.Instance)
			.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);
		_ = await new TenantScopedEventStore(eventInner, UntenantedContext.Instance)
			.LoadAsync("agg-1", "Agg", CancellationToken.None);
		_ = await new TenantScopedProjectionStore<TenantScopedDecoratorTestProjection>(projectionInner, UntenantedContext.Instance)
			.GetByIdAsync("id-1", CancellationToken.None);

		A.CallTo(() => sagaInner.LoadAsync<SagaState>(A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => eventInner.LoadAsync("agg-1", "Agg", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => projectionInner.GetByIdAsync("id-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}
}

/// <summary>
/// A projection type for the generic tenant-scoping decorator arms.
/// </summary>
/// <remarks>
/// Top level and public, deliberately: the dynamic-proxy fake of <c>IProjectionStore&lt;T&gt;</c> cannot
/// reach an internal type, and a public nested type is a nesting violation.
/// </remarks>
public sealed class TenantScopedDecoratorTestProjection
{
	/// <summary>Gets or sets the projection identifier.</summary>
	public string Id { get; set; } = string.Empty;
}
