// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Sharding;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Binds the tenant-scoped decorators to one empty-tenant predicate: the same ambient value must be
/// refused by the decorator and by the scope resolution inside the store it wraps.
/// </summary>
/// <remarks>
/// <para>
/// A whitespace tenant is refused by <c>TenantScope.Scoped</c>, which every first-party store resolves
/// through. A decorator testing only for null or empty lets that value past its own check and the
/// operation fails one layer down instead — same exception type, different provenance, and a consumer
/// store that does not resolve a scope would not fail at all.
/// </para>
/// <para>
/// Both arms: whitespace is refused, and a real tenant still passes. Refusal alone is satisfied by a
/// decorator that rejects everything.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TenantScopedDecoratorPredicateShould
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task RefuseAnAmbientTenantThatIsNotARealTerm(string? ambient)
	{
		var store = new TenantScopedSagaStore(A.Fake<ISagaStore>(), new AmbientTenantContext(ambient));

		_ = await Should.ThrowAsync<TenantRequiredException>(
			async () => await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None),
			"the decorator must refuse the same values TenantScope.Scoped refuses, or a whitespace tenant "
			+ "passes this check and fails inside the store instead");
	}

	[Fact]
	public async Task PassARealTenantThrough()
	{
		var inner = A.Fake<ISagaStore>();
		var store = new TenantScopedSagaStore(inner, new AmbientTenantContext("tenant-a"));

		_ = await store.LoadAsync<SagaState>(Guid.NewGuid(), CancellationToken.None);

		A.CallTo(() => inner.LoadAsync<SagaState>(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private sealed class AmbientTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}
}
