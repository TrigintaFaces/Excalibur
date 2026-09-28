// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.EventSourcing;

namespace Boundary.Tests;

/// <summary>
/// Every provider projection store must record how far it has been folded.
/// <para>
/// <b>What breaks without this.</b> Applying an event to a projection is load-modify-write. A store that
/// records nothing about which events are already in it cannot refuse a second application, so a batch
/// re-delivered after a restart or a reconnect is folded twice. An assigning projection survives that; an
/// accumulating one double-counts, silently and without bound, and nothing downstream can detect it.
/// <see cref="IPositionedProjectionStore{TProjection}"/> is what makes the second application refusable.
/// </para>
/// <para>
/// <b>Why a guard and not a code review.</b> The capability is OPTIONAL by construction — it is resolved
/// through <see cref="IServiceProvider.GetService(Type)"/>, so a store that omits it compiles, registers,
/// passes its own tests and serves reads correctly. The omission is invisible at every gate except this
/// one. A NINTH provider added next year gets the same answer from the compiler that the eight got, and
/// the only thing standing between that and a silent double-count is this census.
/// </para>
/// <para>
/// <b>Scope, stated so the exclusion is visible.</b> The census covers provider stores — the types that
/// talk to a database — and deliberately excludes the core framework assembly, which holds decorators,
/// capability views and the tenant router. Those must not DECLARE the capability: they mediate it, by
/// returning a wrapping view only when the store they wrap actually provides it. A decorator that declared
/// it outright would advertise behaviour its inner store may be unable to perform, which is the failure the
/// deny-by-default resolution in <c>IsolatingProjectionStoreDecorator</c> exists to prevent.
/// </para>
/// </summary>
public sealed class ProjectionStoresRecordTheirPositionShould
{
	/// <summary>
	/// The assembly holding decorators, capability views and the tenant router — mediators, not providers.
	/// </summary>
	private const string CoreAssemblyName = "Excalibur.EventSourcing";

	/// <summary>
	/// A store that must always be discovered. Without it, a census that finds nothing — a renamed
	/// assembly, an unloaded dependency, a moved namespace — would report a clean sweep over zero types,
	/// which is the shape of a guard that cannot fail.
	/// </summary>
	private const string CensusControlType = "SqlServerProjectionStore`1";

	[Fact]
	public void Discover_the_provider_stores_it_claims_to_guard()
	{
		var stores = Census();

		stores.ShouldNotBeEmpty(
			"the census found no provider projection stores at all, so every assertion below would "
			+ "pass over an empty set. The discovery rule is broken, not the tree.");

		stores.Select(static s => s.Name).ShouldContain(
			CensusControlType,
			$"the census did not find {CensusControlType}, which is known to exist and to implement the "
			+ "contract. Whatever the other arms report, they measured an incomplete set.");
	}

	[Fact]
	public void Provide_the_positioned_write_from_every_provider_store()
	{
		var unpositioned = Census()
			.Where(static store => !ImplementsPositioned(store))
			.Select(static store => $"{store.Assembly.GetName().Name}::{store.Name}")
			.OrderBy(static name => name, StringComparer.Ordinal)
			.ToArray();

		unpositioned.ShouldBeEmpty(
			"these provider projection stores implement IProjectionStore<T> but not "
			+ "IPositionedProjectionStore<T>, so the apply path falls back to an unconditional write for "
			+ "them. An event re-delivered after a restart is then folded a second time and an "
			+ "accumulating projection double-counts silently. Implement the positioned contract on each: "
			+ string.Join(", ", unpositioned));
	}

	/// <summary>
	/// The core framework assembly must NOT declare the capability on a decorator — it mediates it.
	/// </summary>
	/// <remarks>
	/// The liveness half of the arm above. Without it, "every provider is positioned" could be satisfied by
	/// a census that had quietly grown to include the mediators, or by a decorator declaring the interface
	/// outright — which is the over-advertising this seam refuses. This arm fails if a decorator ever takes
	/// the shortcut.
	/// </remarks>
	[Fact]
	public void Mediate_the_capability_from_the_core_assembly_rather_than_declaring_it()
	{
		var declaring = FrameworkTypes()
			.Where(static type => type.Assembly.GetName().Name == CoreAssemblyName)
			.Where(static type => Classifiable(type, static t => t is { IsClass: true, IsAbstract: false, IsNested: false }))
			.Where(static type => Classifiable(type, ImplementsProjectionStore))
			.Where(static type => Classifiable(type, ImplementsPositioned))
			.Select(static type => type.Name)
			.OrderBy(static name => name, StringComparer.Ordinal)
			.ToArray();

		declaring.ShouldBeEmpty(
			"these core types declare IPositionedProjectionStore<T> directly. A decorator or router must "
			+ "instead return a mediating view from its capability resolution, and only when the store it "
			+ "wraps actually provides the capability -- declaring it outright advertises behaviour the "
			+ "inner store may be unable to perform: "
			+ string.Join(", ", declaring));
	}

	/// <summary>Provider projection stores: the types that talk to a database.</summary>
	private static Type[] Census() =>
		[.. FrameworkTypes()
			.Where(static type => type.Assembly.GetName().Name != CoreAssemblyName)
			.Where(static type => Classifiable(type, static t => t is { IsClass: true, IsAbstract: false, IsNested: false }))
			.Where(static type => Classifiable(type, ImplementsProjectionStore))
			.OrderBy(static type => type.FullName, StringComparer.Ordinal)];

	/// <summary>
	/// Applies a reflection predicate, treating a type that cannot be inspected as not matching.
	/// </summary>
	/// <remarks>
	/// Inspecting a type can throw when resolving it needs an assembly this test does not carry -- the
	/// analyzer packages reference the compiler, which is not copied here. Those types are not projection
	/// stores, so excluding them is correct; the risk is that a BROADER load failure quietly shrinks the
	/// census to nothing, and that is what the control arm is for.
	/// </remarks>
	private static bool Classifiable(Type type, Func<Type, bool> predicate)
	{
		try
		{
			return predicate(type);
		}
		catch (FileNotFoundException)
		{
			return false;
		}
		catch (TypeLoadException)
		{
			return false;
		}
	}

	private static IEnumerable<Type> FrameworkTypes() =>
		AppDomain.CurrentDomain.GetAssemblies()
			.Where(static a => a.GetName().Name?.StartsWith("Excalibur", StringComparison.Ordinal) == true)
			.SelectMany(SafeTypes);

	/// <summary>
	/// Returns the types an assembly could load, keeping the ones that did.
	/// </summary>
	/// <remarks>
	/// A provider assembly can fail to load a handful of types when an optional dependency is absent.
	/// Losing those is acceptable; throwing away the whole assembly would silently shrink the census, which
	/// is the failure the control above exists to catch.
	/// </remarks>
	private static IEnumerable<Type> SafeTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.OfType<Type>();
		}
	}

	private static bool ImplementsProjectionStore(Type type) =>
		Implements(type, typeof(IProjectionStore<>));

	private static bool ImplementsPositioned(Type type) =>
		Implements(type, typeof(IPositionedProjectionStore<>));

	private static bool Implements(Type type, Type openGeneric) =>
		Array.Exists(
			type.GetInterfaces(),
			i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
}
