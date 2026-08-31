// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Registration locks for the full-stack <c>AddExcaliburSqlServer</c> / <c>AddExcaliburPostgres</c>
/// one-liners, and for the tenancy-capability derivation they depend on.
/// </summary>
/// <remarks>
/// <para>
/// Both metapackages threw <see cref="InvalidOperationException"/> from the registration call itself:
/// the tenant-aware store seam required <em>every</em> public constructor of a store to agree on whether
/// it takes an <see cref="ITenantContext"/>, and the erasure and legal-hold stores each expose a
/// convenience constructor that delegates to the tenant-aware one. Delegation is idiomatic .NET, so the
/// unanimity rule rejected correct code and made the packages uncallable.
/// </para>
/// <para>
/// These are paired deliberately. The registration arms assert the packages are usable (liveness); the
/// marker arms assert the seam still refuses to attest a store that implements no tenancy mechanism
/// (safety). Either arm alone is satisfiable by a broken seam: one by a seam that attests everything,
/// the other by a seam that attests nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class ExcaliburFullStackMetapackageShould : UnitTestBase
{
	// Syntactically valid, never connected to: registration and resolution do not open a connection.
	private const string SqlServerConnectionString =
		"Server=localhost;Database=ExcaliburFullStackTests;Trusted_Connection=True;TrustServerCertificate=True";

	private const string PostgresConnectionString =
		"Host=localhost;Database=excalibur_fullstack_tests;Username=postgres;Password=postgres";

	// The data-subject hasher fails closed without a pepper of at least
	// DataSubjectHashingOptions.MinimumPepperLength characters. That is a real consumer obligation, not
	// part of the defect under test, so these arms satisfy it exactly as a consumer would.
	private const string TestPepper = "metapackage-registration-test-pepper-0123456789";

	[Fact]
	public void RegisterTheSqlServerFullStackWithoutThrowing()
	{
		// The defect: AddExcaliburSqlServer threw before returning, so the package could not be used
		// at all. Registration is the unit under test here, not resolution.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = Should.NotThrow(
			() => services.AddExcaliburSqlServer(sql => sql.ConnectionString = SqlServerConnectionString),
			"AddExcaliburSqlServer is the documented one-line entry point; it must return.");
	}

	[Fact]
	public void RegisterThePostgresFullStackWithoutThrowing()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = Should.NotThrow(
			() => services.AddExcaliburPostgres(pg => pg.ConnectionString = PostgresConnectionString),
			"AddExcaliburPostgres is the documented one-line entry point; it must return.");
	}

	[Fact]
	public void ResolveTheSqlServerErasureStoreFromARealServiceProvider()
	{
		// Registering without throwing is not enough — the registration must also produce a store the
		// container can actually build, through the real provider rather than a service-collection scan.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburSqlServer(sql => sql.ConnectionString = SqlServerConnectionString);
		_ = services.Configure<DataSubjectHashingOptions>(o => o.Pepper = TestPepper);

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IErasureStore>().ShouldNotBeNull();
	}

	[Fact]
	public void ResolveThePostgresErasureStoreFromARealServiceProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburPostgres(pg => pg.ConnectionString = PostgresConnectionString);
		_ = services.Configure<DataSubjectHashingOptions>(o => o.Pepper = TestPepper);

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IErasureStore>().ShouldNotBeNull();
	}

	[Fact]
	public void AttestScopedTenancyForAStoreWhoseConvenienceConstructorDelegates()
	{
		// The shape that broke both metapackages, reduced to its essentials: two public constructors,
		// one of which delegates to the other. The store demonstrably reads the ambient tenant, so the
		// scoped marker is earned and must be emitted.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();

		_ = services.AddTenantAwareStore<IFixtureStore, DelegatingConstructorStore>();

		using var provider = services.BuildServiceProvider();

		provider.GetService<ITenantScopingCapability<IFixtureStore>>().ShouldNotBeNull(
			"a store that takes ITenantContext in its primary constructor reads the ambient tenant, " +
			"and a delegating convenience constructor does not retract that.");
		provider.GetRequiredService<DelegatingConstructorStore>().ShouldNotBeNull();
	}

	[Fact]
	public void StillPreferTheWidestConstructorWhenSeveralTakeTheTenantContext()
	{
		// Guards the regression the explicit-argument threading could otherwise introduce. Several stores
		// registered through the constructing overload expose both a full constructor and a narrower
		// convenience one, BOTH taking the tenant context. Selection must not downgrade to the narrow
		// constructor and silently drop the store's other dependencies.
		//
		// The seam no longer leaves that to a heuristic. Where two constructors both accept the context,
		// the supplied argument cannot distinguish them, so which one wins would otherwise depend on which
		// unrelated services the host happened to register. The intended constructor now says so with
		// [ActivatorUtilitiesConstructor], and this arm proves the marked one is the one selected — the
		// dependency surviving is the observable evidence of that.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		_ = services.AddSingleton<IFixtureDependency, FixtureDependency>();

		_ = services.AddTenantAwareStore<IFixtureStore, TwoTenantConstructorStore>();

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<TwoTenantConstructorStore>();

		store.TenantContext.ShouldNotBeNull();
		store.Dependency.ShouldNotBeNull(
			"the marked constructor is the widest one, so it must win, or stores with a narrow " +
			"tenant-aware convenience constructor would lose their remaining dependencies.");
	}

	[Fact]
	public void RefuseAStoreWhoseSeveralTenantConstructorsNameNoIntendedOne()
	{
		// The other half of the contract above, and the reason the marker is required rather than merely
		// honoured. Identical to TwoTenantConstructorStore except that neither constructor is marked, so
		// nothing identifies which one construction should use.
		//
		// Without this arm the seam could satisfy the previous one by picking the widest constructor on a
		// guess and still be wrong the moment a host stopped registering IFixtureDependency: selection
		// would silently fall back to the narrow constructor, or fail reporting a constructor ambiguity
		// that names neither the store's real problem nor its fix. Refusing at registration is what makes
		// the previous arm's outcome a guarantee instead of a coincidence.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		_ = services.AddSingleton<IFixtureDependency, FixtureDependency>();

		var thrown = Should.Throw<InvalidOperationException>(
			() => services.AddTenantAwareStore<IFixtureStore, UnmarkedTwoTenantConstructorStore>(),
			"two constructors both accepting the ambient tenant context leave the seam's explicit argument " +
			"unable to select one, so the registration must fail while it is being built rather than " +
			"resolve differently depending on what else the host happens to have registered.");

		thrown.Message.ShouldContain(
			"ActivatorUtilitiesConstructor",
			Case.Sensitive,
			"the refusal must name the marker that resolves it, or it reports a problem without its fix.");
	}

	[Fact]
	public void RefuseToAttestScopedTenancyForATenancyBlindStore()
	{
		// The property the seam exists to protect: a store that takes no ITenantContext in ANY
		// constructor implements no ambient tenancy, and must not carry a marker claiming it does.
		// Without this arm, a seam that attested every store would pass every other test in this file.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();

		_ = services.AddTenantAwareStore<IFixtureStore, TenancyBlindStore>();

		using var provider = services.BuildServiceProvider();

		provider.GetService<ITenantScopingCapability<IFixtureStore>>().ShouldBeNull(
			"a store no constructor of which accepts an ITenantContext implements no ambient tenancy; " +
			"attesting one would let it pass the multi-tenancy gate under a claim it does not earn.");
	}

	[Fact]
	public void RefuseToAttestScopedTenancyForAStoreWithOnlyANonTenantConvenienceConstructor()
	{
		// The near-miss of the delegating shape: two constructors, neither taking ITenantContext. The
		// relaxation from "all constructors" to "any constructor" must not turn multiplicity itself
		// into evidence.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();

		_ = services.AddTenantAwareStore<IFixtureStore, TwoConstructorTenancyBlindStore>();

		using var provider = services.BuildServiceProvider();

		provider.GetService<ITenantScopingCapability<IFixtureStore>>().ShouldBeNull(
			"having several constructors is not evidence of tenancy; only taking the context is.");
	}

	[Fact]
	public void ActuallyThreadTheTenantContextIntoADelegatingConstructorStore()
	{
		// Emitting the marker is not the same as honouring it. If the seam let ActivatorUtilities pick the
		// parameterless convenience constructor, the store would be built with no tenant context while
		// carrying a marker saying it reads one — the lying-marker defect, arrived at from the other side.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();

		_ = services.AddTenantAwareStore<IFixtureStore, DelegatingConstructorStore>();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<DelegatingConstructorStore>().TenantContext.ShouldNotBeNull(
			"the scoped marker attests that the store reads the ambient tenant, so the registration must " +
			"build it through the constructor that takes one.");
	}

	[Fact]
	public void ThreadTheTenantContextEvenWhenTheWidestConstructorOmitsIt()
	{
		// ActivatorUtilities prefers the constructor with the most resolvable parameters. Left unaided it
		// would select the two-parameter tenant-blind constructor here and the emitted marker would be a
		// lie. The seam passes the resolved context as an explicit argument, constraining the choice.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		_ = services.AddSingleton<IFixtureDependency, FixtureDependency>();

		_ = services.AddTenantAwareStore<IFixtureStore, WidestConstructorIsTenantBlindStore>();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<WidestConstructorIsTenantBlindStore>().TenantContext.ShouldNotBeNull(
			"a store whose widest constructor omits the tenant context must still be built through the " +
			"constructor that accepts it, or the emitted scoped marker attests something untrue.");
	}
}

/// <summary>Contract for the tenancy-derivation fixtures.</summary>
internal interface IFixtureStore
{
	/// <summary>Gets the ambient tenant context the store was built with, if any.</summary>
	ITenantContext? TenantContext { get; }
}

/// <summary>
/// The shape the erasure and legal-hold stores use: a convenience constructor that delegates to the
/// tenant-aware one. Implements <see cref="IFixtureStore"/> directly, inheriting no base that could
/// supply the member under test.
/// </summary>
internal sealed class DelegatingConstructorStore : IFixtureStore
{
	public DelegatingConstructorStore()
		: this(tenantContext: null)
	{
	}

	public DelegatingConstructorStore(ITenantContext? tenantContext) => TenantContext = tenantContext;

	public ITenantContext? TenantContext { get; }
}

/// <summary>A store implementing no tenancy mechanism at all.</summary>
internal sealed class TenancyBlindStore : IFixtureStore
{
	public ITenantContext? TenantContext => null;
}

/// <summary>Two constructors, neither of which takes the ambient tenant context.</summary>
internal sealed class TwoConstructorTenancyBlindStore : IFixtureStore
{
	public TwoConstructorTenancyBlindStore()
		: this(0)
	{
	}

	public TwoConstructorTenancyBlindStore(int unused) => Unused = unused;

	public int Unused { get; }

	public ITenantContext? TenantContext => null;
}


/// <summary>An ordinary resolvable dependency, used to widen a tenant-blind constructor.</summary>
internal interface IFixtureDependency;

/// <inheritdoc cref="IFixtureDependency"/>
internal sealed class FixtureDependency : IFixtureDependency;

/// <summary>
/// A store whose <em>widest</em> public constructor takes no tenant context. Unaided, ActivatorUtilities
/// would prefer that constructor; the seam must still build through the tenant-aware one.
/// </summary>
internal sealed class WidestConstructorIsTenantBlindStore : IFixtureStore
{
	public WidestConstructorIsTenantBlindStore(IFixtureDependency first, IFixtureDependency second)
	{
		First = first;
		Second = second;
	}

	public WidestConstructorIsTenantBlindStore(ITenantContext tenantContext) => TenantContext = tenantContext;

	public IFixtureDependency? First { get; }

	public IFixtureDependency? Second { get; }

	public ITenantContext? TenantContext { get; }
}
/// <summary>
/// Two public constructors, <em>both</em> taking the tenant context — one full, one narrow. Models the
/// real stores registered through the constructing overload.
/// </summary>
internal sealed class TwoTenantConstructorStore : IFixtureStore
{
	public TwoTenantConstructorStore(ITenantContext tenantContext)
		: this(tenantContext, dependency: null)
	{
	}

	// Both constructors accept the tenant context AND both are satisfiable from the container, so the
	// supplied context alone cannot pick one. The seam requires the intended constructor to say so.
	[ActivatorUtilitiesConstructor]
	public TwoTenantConstructorStore(ITenantContext tenantContext, IFixtureDependency? dependency)
	{
		TenantContext = tenantContext;
		Dependency = dependency;
	}

	public IFixtureDependency? Dependency { get; }

	public ITenantContext? TenantContext { get; }
}

/// <summary>
/// <see cref="TwoTenantConstructorStore"/> with neither constructor marked — the shape the seam refuses,
/// because nothing names the constructor construction should use.
/// </summary>
internal sealed class UnmarkedTwoTenantConstructorStore : IFixtureStore
{
	public UnmarkedTwoTenantConstructorStore(ITenantContext tenantContext)
		: this(tenantContext, dependency: null)
	{
	}

	public UnmarkedTwoTenantConstructorStore(ITenantContext tenantContext, IFixtureDependency? dependency)
	{
		TenantContext = tenantContext;
		Dependency = dependency;
	}

	public IFixtureDependency? Dependency { get; }

	public ITenantContext? TenantContext { get; }
}
