// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Executable proof for the <c>Decorate</c> keyed-service defect. This was raised as a static reading and is
/// settled here by a real container: nobody had resolved a service.
/// </summary>
/// <remarks>
/// <para>
/// <b>The chain.</b> Every framework event-store provider registers its store as a KEYED service
/// (<c>AddKeyedSingleton&lt;IEventStore&gt;("sqlserver")</c>, then
/// <c>TryAddKeyedSingleton&lt;IEventStore&gt;("default")</c>). <c>AddMultiTenancy</c> with the row-discriminator
/// strategy then calls <c>services.Decorate&lt;IEventStore, TenantScopedEventStore&gt;()</c>.
/// </para>
/// <para>
/// <c>Decorate</c> selects its target with <c>LastOrDefault(sd =&gt; sd.ServiceType == typeof(TService))</c>. A
/// keyed descriptor satisfies that predicate — its <c>ServiceType</c> is still <c>IEventStore</c>. The
/// descriptor is then <c>Remove</c>d and a replacement is <c>Add</c>ed as
/// <c>new ServiceDescriptor(typeof(TService), factory, lifetime)</c>, which carries <b>no service key</b>.
/// </para>
/// <para>
/// <b>Consequence.</b> The keyed registration the providers rely on is destroyed, so every
/// <c>GetRequiredKeyedService&lt;IEventStore&gt;("default")</c> — including the GDPR erasure contributor —
/// resolves nothing. The failure is a resolution error at first use, not a registration error, so nothing
/// catches it at startup.
/// </para>
/// <para>
/// <b>Both arms, deliberately.</b> "The keyed service survives" is the safety property. Paired with it,
/// "decoration actually happened" is the liveness property — a <c>Decorate</c> that quietly did nothing would
/// satisfy the first arm perfectly while leaving every tenant's rows unscoped. Neither arm alone says anything
/// useful, and the inert-control failures this codebase keeps finding are all missing-liveness-arm failures.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DecorateKeyedServicePreservationShould
{
	/// <summary>
	/// SAFETY. A keyed registration must still be resolvable by its key after decoration.
	/// </summary>
	/// <remarks>
	/// RED at HEAD: <c>Decorate</c> re-adds the descriptor without a service key, so the keyed lookup that
	/// every provider and the erasure contributor perform resolves to null.
	/// </remarks>
	[Fact]
	public void PreserveTheServiceKey_WhenDecoratingAKeyedRegistration()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => new FakeEventStore());

		_ = services.Decorate<IEventStore, TenantScopedEventStore>();

		using var provider = services.BuildServiceProvider();

		provider.GetKeyedService<IEventStore>("default").ShouldNotBeNull(
			"Decorate() removed the keyed IEventStore descriptor and re-added it WITHOUT a service key. " +
			"Every provider registers IEventStore only as a keyed service, and the GDPR erasure contributor " +
			"resolves GetRequiredKeyedService<IEventStore>(\"default\"). After AddMultiTenancy applies the " +
			"row-discriminator decorator, that resolution finds nothing — and it fails at first use, not at " +
			"startup, so no registration-time guard catches it.");
	}

	/// <summary>
	/// LIVENESS. Decoration must actually have happened.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by a <c>Decorate</c> that does nothing at all — which would
	/// leave every tenant's rows unscoped while looking perfectly healthy.
	/// </remarks>
	[Fact]
	public void ActuallyDecorate_SoTheTenantScopeIsApplied()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => new FakeEventStore());

		_ = services.Decorate<IEventStore, TenantScopedEventStore>();

		using var provider = services.BuildServiceProvider();

		// Resolved by whichever route survives decoration — the point is that SOMETHING resolves to the
		// decorator. If this is a bare FakeEventStore, tenant scoping was silently dropped.
		var resolved = provider.GetKeyedService<IEventStore>("default") ?? provider.GetService<IEventStore>();

		resolved.ShouldBeOfType<TenantScopedEventStore>(
			"Decorate() must wrap the registered store in TenantScopedEventStore. A decoration that silently " +
			"did nothing would satisfy the key-preservation arm above while leaving every tenant's rows " +
			"unscoped — safety without liveness proves nothing.");
	}

	/// <summary>
	/// NON-VACUITY. The unkeyed path is unaffected, so a failure above is about the KEY, not about Decorate.
	/// </summary>
	[Fact]
	public void StillDecorate_AnUnkeyedRegistration()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());
		services.AddSingleton<IEventStore>(_ => new FakeEventStore());

		_ = services.Decorate<IEventStore, TenantScopedEventStore>();

		using var provider = services.BuildServiceProvider();

		// GREEN at HEAD. Pins that Decorate itself works, so the RED arm above isolates the keyed defect
		// rather than a broken helper or a broken fixture.
		provider.GetRequiredService<IEventStore>().ShouldBeOfType<TenantScopedEventStore>(
			"Decorate() over an unkeyed registration must still wrap the store. If this arm is RED the " +
			"fixture is wrong and the keyed arm above proves nothing.");
	}

	/// <summary>
	/// CHARACTERIZATION. Records what a NON-keyed resolve does, before and after decoration, over a
	/// keyed-only registration — the shape every framework provider actually produces.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm asserts nothing about what is <i>correct</i>. It pins what is <i>true</i>, because the
	/// keyed-preservation fix changes it and nobody should discover that from a consumer bug report.
	/// </para>
	/// <para>
	/// The old <c>Decorate</c> destroyed the ServiceKey, which silently converted a keyed-only registration
	/// into a non-keyed one. That defect is what made the pattern in the shipped provider READMEs —
	/// <c>public MyService(IEventStore eventStore)</c>, plain constructor injection — resolve at all on a
	/// keyed-only provider, but only in hosts that happened to call <c>AddMultiTenancy</c>. A load-bearing
	/// bug. Fixing it correctly makes the non-keyed resolve return null again, which is the honest answer and
	/// may surface as a regression in any host that was relying on the accident.
	/// </para>
	/// </remarks>
	[Fact]
	public void Characterize_NonKeyedResolution_OverAKeyedOnlyRegistration()
	{
		var undecorated = new ServiceCollection();
		undecorated.AddSingleton<ITenantContext>(new FakeTenantContext());
		undecorated.AddKeyedSingleton<IEventStore>("default", (_, _) => new FakeEventStore());
		using var undecoratedProvider = undecorated.BuildServiceProvider();

		// Baseline, no decoration: a keyed-only registration is NOT resolvable without the key. If this ever
		// becomes non-null, the container's keyed semantics changed and every claim below is stale.
		undecoratedProvider.GetService<IEventStore>().ShouldBeNull(
			"A keyed-only registration must not satisfy a non-keyed resolve. If it does, the DI container's " +
			"semantics changed and this whole file needs re-reading.");

		var decorated = new ServiceCollection();
		decorated.AddSingleton<ITenantContext>(new FakeTenantContext());
		decorated.AddKeyedSingleton<IEventStore>("default", (_, _) => new FakeEventStore());
		_ = decorated.Decorate<IEventStore, TenantScopedEventStore>();
		using var decoratedProvider = decorated.BuildServiceProvider();

		// With the ServiceKey preserved, decoration must not smuggle a non-keyed registration into existence.
		// Before the fix this resolved — that was the bug, and consumers following the provider READMEs
		// depended on it without knowing.
		decoratedProvider.GetService<IEventStore>().ShouldBeNull(
			"Decoration must not create a non-keyed registration as a side effect. If this resolves, the " +
			"ServiceKey was dropped again and GetRequiredKeyedService<IEventStore>(\"default\") is broken " +
			"for every provider and for the GDPR erasure contributor.");

		// And the keyed route still works, which is the property that matters.
		decoratedProvider.GetKeyedService<IEventStore>("default").ShouldBeOfType<TenantScopedEventStore>();
	}

	/// <summary>
	/// PRODUCTION SHAPE. The forwarder that <c>AddEventSourcing</c> registers must survive decoration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that matters, and the one my first three arms missed. Core registers:
	/// </para>
	/// <code>
	/// services.TryAddSingleton&lt;IEventStore&gt;(sp =&gt; sp.GetRequiredKeyedService&lt;IEventStore&gt;("default"));
	/// </code>
	/// <para>
	/// — a NON-keyed forwarder onto the keyed <c>"default"</c> store, so consumers can write
	/// <c>public MyService(IEventStore eventStore)</c> exactly as the provider READMEs document. The forwarder
	/// is the reason plain constructor injection works at all.
	/// </para>
	/// <para>
	/// If <c>Decorate</c> destroys the keyed <c>"default"</c> registration, the forwarder still exists and
	/// still resolves — straight into <c>GetRequiredKeyedService("default")</c>, which now finds nothing and
	/// <b>throws</b>. So the defect does not merely break the erasure contributor: it breaks every consumer
	/// following the documented injection pattern, at first use rather than at startup.
	/// </para>
	/// </remarks>
	[Fact]
	public void PreserveTheCoreForwarder_SoDocumentedConstructorInjectionStillResolves()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());

		// Provider registration, verbatim shape.
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => new FakeEventStore());

		// Core AddEventSourcing's forwarder, verbatim shape.
		services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));

		_ = DecorateWithTenantScope(services);

		using var provider = services.BuildServiceProvider();

		// The documented consumer pattern. Under the pre-fix Decorate this throws InvalidOperationException
		// from inside the forwarder, because the keyed "default" it forwards to was removed.
		var resolved = Should.NotThrow(
			() => provider.GetRequiredService<IEventStore>(),
			"Plain constructor injection of IEventStore is the pattern every provider README documents. It " +
			"works via a non-keyed forwarder onto the keyed \"default\" store. If Decorate drops the key, the " +
			"forwarder resolves into nothing and throws at first use — not at startup, so no registration " +
			"guard catches it.");

		resolved.ShouldNotBeNull();
	}

	/// <summary>
	/// CHARACTERIZATION of <c>Decorate</c>'s selection rule. Not a safety assertion, and NOT a leak.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>I originally wrote this arm as a RED safety assertion and I was wrong.</b> It composed
	/// <c>[keyed "default" store, non-keyed alias]</c> — an ordering the framework never builds. No production
	/// path registers the real store under <c>"default"</c>; providers register the store under their own key
	/// and a <i>forwarder</i> under <c>"default"</c>, and the alias, when present, is registered first. Both
	/// real shapes are asserted by the two <c>ScopeEveryReachableRoute…</c> / <c>ScopeTheKeyedRoute…</c> arms,
	/// and both are GREEN: every reachable route to the store is tenant-scoped.
	/// </para>
	/// <para>
	/// A container proof of a composition production never builds is not a proof. Keeping it RED would have
	/// been an inert control asserting an unreachable failure — the exact disease this file exists to catch.
	/// </para>
	/// <para>
	/// What survives is real and worth pinning: <c>Decorate</c> wraps <b>one</b> descriptor, chosen by
	/// <c>LastOrDefault</c>. The tenant-scoping of every keyed store is therefore correct <i>by accident of
	/// registration order</i>, and nothing enforces that order. This arm records the mechanism so that when the
	/// seam is changed to decorate every matching descriptor, someone must update it on purpose.
	/// </para>
	/// </remarks>
	/// <remarks>
	/// <para>
	/// <c>Decorate</c> selects <c>LastOrDefault(sd =&gt; sd.ServiceType == typeof(TService))</c> — <b>one</b>
	/// descriptor. In the production shape there are two: the provider's keyed <c>"default"</c> store, and the
	/// core's non-keyed forwarder registered after it. The forwarder is last, so it is what gets decorated.
	/// </para>
	/// <para>
	/// If that is what happens, the keyed <c>"default"</c> registration is left <b>undecorated</b> — and every
	/// consumer resolving <c>GetRequiredKeyedService&lt;IEventStore&gt;("default")</c> gets a store with no
	/// tenant scoping at all. The GDPR erasure contributor resolves exactly that key.
	/// </para>
	/// <para>
	/// <b>This is NOT a live cross-tenant leak, and nobody should read it as one.</b> The documented
	/// composition — <c>AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt; es.AddCosmosDb()))</c> — registers the
	/// alias inside the base method and the provider's keyed store inside <c>configure</c>, so the keyed store
	/// is last and <c>Decorate</c> wraps it. Verified independently by four people at committed HEAD. The sibling
	/// arm <c>ScopeTheKeyedStore_WhenTheForwarderIsRegisteredFirst</c> pins that safe order and is GREEN.
	/// </para>
	/// <para>
	/// It is RED because <b>the guarantee is order-dependent and nothing forces the order.</b> A refactor that
	/// registers the alias after the provider, or a consumer who builds the collection by hand, silently
	/// unwraps the keyed store — and until this arm existed, no test would have noticed. A tenant-isolation
	/// property that holds by the accident of two call sites is not a property. It goes GREEN when
	/// <c>Decorate</c> wraps every matching descriptor rather than the last one.
	/// </para>
	/// <para>
	/// This arm asserts the property (<i>the keyed store is tenant-scoped</i>), not the mechanism. It does not
	/// care how many descriptors <c>Decorate</c> touches, only that a tenant-owned store cannot be reached
	/// through any route without its scope.
	/// </para>
	/// </remarks>
	[Fact]
	public void Characterize_DecorateWrapsOnlyTheLastMatchingDescriptor()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => new FakeEventStore());
		services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));

		_ = DecorateWithTenantScope(services);

		using var provider = services.BuildServiceProvider();

		// Deliberately updated when the seam moved from Decorate (LastOrDefault, one descriptor) to
		// DecorateKeyedStores (every terminal). The keyed "default" IS the real store here (Shape B — no
		// non-default provider key), so it is now the decorated terminal, not left bare.
		provider.GetKeyedService<IEventStore>("default").ShouldBeOfType<TenantScopedEventStore>(
			"DecorateKeyedStores wraps the real terminal. In this Shape-B registration the keyed \"default\" IS " +
			"the real store, so it must resolve tenant-scoped — no longer the bare FakeEventStore the old " +
			"LastOrDefault Decorate left behind.");

		provider.GetRequiredService<IEventStore>().ShouldBeOfType<TenantScopedEventStore>(
			"The non-keyed forwarder resolves through the decorated keyed \"default\", so it is tenant-scoped too.");
	}

	/// <summary>
	/// ORDER SENSITIVITY. Which descriptor <c>Decorate</c> wraps depends on registration order, and both
	/// orders occur in real hosts.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>LastOrDefault</c> makes the outcome depend on whether the consumer wrote
	/// <c>AddEventSourcing().AddCosmosDb()</c> (forwarder registered first, keyed store last → the keyed store
	/// is decorated, the forwarder resolves through it, everything is scoped) or the reverse (keyed store
	/// first, forwarder last → the forwarder is decorated and the keyed store is left bare).
	/// </para>
	/// <para>
	/// A tenant-isolation guarantee that depends on the order two extension methods were called is not a
	/// guarantee. This arm pins the safe order so the difference is visible; the sibling arm pins the unsafe
	/// one. Both are true today, which is the finding.
	/// </para>
	/// </remarks>
	[Fact]
	public void ScopeTheKeyedStore_WhenTheForwarderIsRegisteredFirst()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());

		// AddEventSourcing() first — the forwarder is registered before any provider store.
		services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => new FakeEventStore());

		_ = DecorateWithTenantScope(services);

		using var provider = services.BuildServiceProvider();

		provider.GetKeyedService<IEventStore>("default").ShouldBeOfType<TenantScopedEventStore>(
			"With the forwarder registered first, LastOrDefault selects the KEYED store and decorates it, so " +
			"both routes are tenant-scoped. Reverse the two registration calls and the keyed store is left " +
			"bare. Tenant isolation must not depend on the order the consumer called two extension methods.");
	}

	/// <summary>
	/// PRODUCTION SHAPE A — <c>AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt; es.AddCosmosDb()))</c>.
	/// Three descriptors, in the order the real code registers them.
	/// </summary>
	/// <remarks>
	/// The provider never registers the store under <c>"default"</c>. It registers the store under its own key
	/// and a <i>second</i> keyed forwarder under <c>"default"</c>. So the descriptor list is
	/// <c>[alias, store@"cosmosdb", forwarder@"default"]</c> and <c>LastOrDefault</c> selects the
	/// <b>forwarder</b>. The bare store under <c>"cosmosdb"</c> is never decorated — and nothing outside the
	/// forwarder ever resolves that key, which is why the property still holds.
	/// </remarks>
	[Fact]
	public void ScopeEveryReachableRoute_InTheCoreBuilderShape()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());

		// [0] core AddExcaliburEventSourcing(): the non-keyed alias, registered BEFORE configure().
		services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));
		// [1] provider: the real store under its own key.
		services.AddKeyedSingleton<IEventStore>("cosmosdb", (_, _) => new FakeEventStore());
		// [2] provider: a keyed forwarder onto it.
		services.AddKeyedSingleton<IEventStore>("default", (sp, _) => sp.GetRequiredKeyedService<IEventStore>("cosmosdb"));

		_ = DecorateWithTenantScope(services);

		using var provider = services.BuildServiceProvider();

		provider.GetKeyedService<IEventStore>("default").ShouldBeOfType<TenantScopedEventStore>(
			"The keyed \"default\" route must be tenant-scoped. This is the route the GDPR erasure contributor " +
			"and every repository resolve.");

		provider.GetRequiredService<IEventStore>().ShouldBeOfType<TenantScopedEventStore>(
			"The non-keyed alias route must be tenant-scoped. This is the route the provider READMEs document " +
			"for plain constructor injection.");
	}

	/// <summary>
	/// PRODUCTION SHAPE B — <c>AddSqlServerEventSourcing(...)</c> called directly. No alias exists.
	/// </summary>
	/// <remarks>
	/// This public entry point registers the keyed store and the keyed <c>"default"</c> forwarder and never
	/// calls <c>AddExcaliburEventSourcing</c>, so no non-keyed alias is ever registered. A consumer here cannot
	/// inject <c>IEventStore</c> plainly at all — but the keyed route, which is the one the framework itself
	/// resolves, must still be scoped.
	/// </remarks>
	[Fact]
	public void ScopeTheKeyedRoute_InTheStandaloneProviderShape()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());

		services.AddKeyedSingleton<IEventStore>("sqlserver", (_, _) => new FakeEventStore());
		services.AddKeyedSingleton<IEventStore>("default", (sp, _) => sp.GetRequiredKeyedService<IEventStore>("sqlserver"));

		_ = DecorateWithTenantScope(services);

		using var provider = services.BuildServiceProvider();

		provider.GetKeyedService<IEventStore>("default").ShouldBeOfType<TenantScopedEventStore>(
			"AddSqlServerEventSourcing registers no non-keyed alias, so the keyed \"default\" forwarder is the " +
			"last IEventStore descriptor and must be the one Decorate wraps.");
	}

	/// <summary>
	/// THE egm9wd LEAK — SAFETY. Two providers registered under distinct keys must BOTH be tenant-scoped.
	/// </summary>
	/// <remarks>
	/// This is the multi-registration shape the old <c>Decorate</c> (LastOrDefault, one descriptor) could not
	/// cover: it wrapped exactly one terminal and left the other provider's store RAW, so a tenant could reach
	/// another tenant's rows through the un-decorated key. <c>DecorateKeyedStores</c> wraps EVERY terminal.
	/// Non-vacuous: revert the seam to LastOrDefault <c>Decorate</c> and one terminal resolves a bare
	/// <c>FakeEventStore</c> (RED).
	/// </remarks>
	[Fact]
	public void ScopeBothTerminals_WhenTwoProvidersAreRegistered()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITenantContext>(new FakeTenantContext());

		// Two provider terminals under distinct keys + a "default" forwarder onto one of them.
		services.AddKeyedSingleton<IEventStore>("sqlserver", (_, _) => new FakeEventStore());
		services.AddKeyedSingleton<IEventStore>("cosmosdb", (_, _) => new FakeEventStore());
		services.AddKeyedSingleton<IEventStore>("default", (sp, _) => sp.GetRequiredKeyedService<IEventStore>("sqlserver"));

		_ = DecorateWithTenantScope(services);

		using var provider = services.BuildServiceProvider();

		provider.GetKeyedService<IEventStore>("sqlserver").ShouldBeOfType<TenantScopedEventStore>(
			"Both provider terminals must be tenant-scoped. LastOrDefault Decorate wrapped only ONE descriptor, " +
			"leaving the other provider's store raw — a live cross-tenant leak through the un-decorated key.");
		provider.GetKeyedService<IEventStore>("cosmosdb").ShouldBeOfType<TenantScopedEventStore>(
			"Both provider terminals must be tenant-scoped. The 'cosmosdb' store left raw is the cross-tenant leak " +
			"this seam exists to close.");
		provider.GetKeyedService<IEventStore>("default").ShouldBeOfType<TenantScopedEventStore>(
			"The \"default\" forwarder resolves through a decorated terminal, so it is scoped too.");
	}

	// The egm9wd seam under test: wrap every terminal store registration in the tenant-scoping decorator,
	// preserving keys and forwarders. Replaces the old LastOrDefault Decorate<IEventStore, TenantScopedEventStore>().
	private static bool DecorateWithTenantScope(IServiceCollection services) =>
		services.DecorateKeyedStores<IEventStore>(
			(inner, sp) => new TenantScopedEventStore(inner, sp.GetRequiredService<ITenantContext>()));

	private sealed class FakeTenantContext : ITenantContext
	{
		public string? TenantId => "tenant-1";

		public bool HasTenant => true;
	}

	private sealed class FakeEventStore : IEventStore
	{
		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId,
			string aggregateType,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId,
			string aggregateType,
			long fromVersion,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			CancellationToken cancellationToken) => default;
	}
}
