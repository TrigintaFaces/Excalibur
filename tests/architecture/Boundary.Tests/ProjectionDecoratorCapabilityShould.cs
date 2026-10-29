// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Decorators;

namespace Boundary.Tests;

/// <summary>Decorating a projection store must not hide the capabilities of the store it wraps.</summary>
/// <remarks>
/// <para>
/// THE REQUIREMENT. <see cref="IProjectionStore{TProjection}"/> supplies a default interface
/// implementation of <see cref="IServiceProvider.GetService(Type)"/> that answers for the instance
/// ITSELF. That is right for a terminal store and silently wrong for a decorator: a decorator which does
/// not supply its own answers about itself, so every capability of the store underneath it -- destination
/// identity, positioned writes, visibility barrier, destination scope -- disappears for every provider
/// beneath the decorator. Nothing throws. The obligation is stated in the interface's own documentation
/// ("Decorators override it to answer for the store they wrap"), and prose does not fire.
/// </para>
/// <para>
/// THE PREDICATE ACTUALLY TESTED. A type counts as a decorator when it (a) implements
/// <see cref="IProjectionStore{TProjection}"/> and is not itself an interface, and (b) OBTAINS A
/// PROJECTION STORE OTHER THAN ITSELF -- a constructor parameter, field, or property whose type is a
/// projection store, or any generic argument or array element of one, so a resolver, factory, or
/// <see cref="Lazy{T}"/> yielding a store counts -- looking at its own declared members and those of
/// every base class. Such a type must have a concrete GetService declared on itself or on one of its base
/// classes, rather than inheriting the interface's default.
/// <para>
/// "Other than itself" is not a refinement for tidiness; without it the predicate matched all eight
/// terminal provider stores, because each declares an <c>ILogger&lt;TheStoreItself&gt;</c> whose generic
/// argument is a projection store. See <c>ContainsStore</c> for the measurement and the cost.
/// </para>
/// </para>
/// <para>
/// HOW THEY DIFFER. The requirement is about intent (this type decorates a store); the predicate is
/// about structure (this type holds a store). The predicate is deliberately the wider of the two, and is
/// keyed on SHAPE rather than on a name suffix, because a suffix stops working the moment somebody names
/// a decorator differently. Being wider, it also reaches the mediating capability views, which hold the
/// decorator they defer to rather than an inner store; that exemption is named below rather than
/// silently filtered by shape.
/// </para>
/// <para>
/// This checks the exposed capability-resolution seam, not forwarding behaviour. The decorator unit and
/// integration tests separately verify that each mediated capability routes correctly.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class ProjectionDecoratorCapabilityShould
{
	private const BindingFlags Members =
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	/// <summary>
	/// The one capability view this guard cannot reach.
	/// <para>
	/// THE SHARED BASE IS NO LONGER EXEMPT, AND THAT IS THIS GUARD'S PROOF. The mediating capability
	/// views are projection stores holding the DECORATOR they defer to, so the structural predicate
	/// reaches them -- and <see cref="ProjectionStoreCapabilityView{TProjection}"/> now supplies the
	/// capability-resolution seam itself, answering for the view's own capability and otherwise
	/// delegating to the decorator. Every view derived from it therefore satisfies the obligation above
	/// as an ordinary decorator does, BY CONSTRUCTION rather than by a named exemption, and the six
	/// derived views are enforced here instead of excused. Removing the base from this list is what
	/// turns that fix into a measurement: were the seam deleted, the views would be reported as
	/// violations by the main assertion below.
	/// </para>
	/// <para>
	/// The remaining entry is the one anomaly the measurement turned up, and it is listed individually on
	/// purpose: it implements its capability interface directly and reimplements the inherited store
	/// surface by hand, so it is a capability view that does not say so structurally, and the shared
	/// base's seam cannot reach it. Its capability interface DOES inherit the store interface, so it is
	/// probeable and the obligation is genuinely its own -- unlike the destination-identity and
	/// visibility-barrier views beside it, which implement only a capability interface that does not
	/// inherit <see cref="IServiceProvider"/> and so cannot be probed at all. Folding it onto the shared
	/// base would remove this entry, and that remains the resolution -- it is deliberately left visible
	/// instead of being absorbed by a wider rule. It is a private nested type, so it cannot be referenced
	/// with <c>typeof</c>.
	/// </para>
	/// <para>
	/// NARROWED GAP, stated rather than implied: a new capability view derived from the shared base now
	/// inherits the correct answer rather than an exemption. Only a second hand-rolled view has to be
	/// added here deliberately -- and adding one is the decision this list exists to make visible.
	/// </para>
	/// </summary>
	/// <summary>
	/// Deliberately EMPTY. Every capability view now resolves capabilities for the store it wraps, so none
	/// is excused from this guard.
	/// </summary>
	/// <remarks>
	/// This list held two entries, then one, then none, as each view was fixed rather than excused. It is
	/// kept as an empty array instead of being deleted because an exemption mechanism that still exists but
	/// matches nothing is falsifiable — adding a name to it is a visible, reviewable act — whereas deleting
	/// the mechanism would let the next person reintroduce an exemption with no precedent to argue against.
	/// An exemption here is the one place a silently dropped capability could still ship, so it should cost
	/// an explicit edit and a justification.
	/// </remarks>
	private static readonly string[] ExemptCapabilityViews = [];

	[Fact]
	public void DeclareCapabilityResolutionOnEveryShippedProjectionStoreDecorator()
	{
		var assemblies = BoundaryFrameworkAssemblyFixture.GetExpectedAssemblies(AppContext.BaseDirectory,
			Path.Combine(AppContext.BaseDirectory, "Boundary.Tests.deps.json"));
		// Load/type-resolution failures must fail, never silently reduce the population.
		var types = assemblies.SelectMany(a => Assembly.LoadFrom(a.Path).GetTypes()).ToArray();

		var names = FindDecorators(types, typeof(IProjectionStore<>))
			.Select(t => t.FullName!)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToArray();

		// NON-VACUITY, FAILURE MODE 1 of 2 -- A PREDICATE THAT MATCHES NOTHING WOULD PASS FOREVER.
		// Every shipped decorator is asserted by name, so a predicate that stopped matching one of them
		// fails here rather than quietly shrinking the population it enforces over.
		foreach (var expected in new[]
		{
			"Excalibur.EventSourcing.Decorators.IsolatingProjectionStoreDecorator`1",
			"Excalibur.EventSourcing.Encryption.Decorators.EncryptingProjectionStoreDecorator`1",
			"Excalibur.EventSourcing.Sharding.TenantRoutingProjectionStore`1",
			"Excalibur.EventSourcing.Sharding.TenantScopedProjectionStore`1",
			// The shared capability-view base and the views over it, no longer exempt. Naming them here
			// is what stops the exemption being reinstated by accident: were the base added back to
			// ExemptCapabilityViews, these would drop out of the population and this assertion fails,
			// rather than the guard quietly shrinking to the four decorators it started with.
			"Excalibur.EventSourcing.Decorators.ProjectionStoreCapabilityView`1",
			"Excalibur.EventSourcing.Encryption.Decorators.EncryptingProjectionStoreDecorator`1+EncryptingPositionedView",
			"Excalibur.EventSourcing.Sharding.TenantScopedProjectionStore`1+TenantScopedPositionedView",
			// The last view to carry an exemption. Pinned by name so that re-exempting it, or losing it from
			// the population, reddens this arm instead of quietly shrinking what the guard covers.
			"Excalibur.EventSourcing.Sharding.TenantRoutingProjectionStore`1+TenantRoutingPositionedView",
		})
		{
			names.ShouldContain(expected, $"discovered decorators: {string.Join(", ", names)}");
		}

		// The predicate must not drag in terminal provider stores, for which the interface's default is the
		// CORRECT answer. An over-matching predicate is green until the first terminal store relies on it.
		names.ShouldNotContain("Excalibur.EventSourcing.SqlServer.SqlServerProjectionStore`1",
			$"discovered decorators: {string.Join(", ", names)}");

		FindViolations(types, typeof(IProjectionStore<>))
			.Select(t => t.FullName!)
			.ShouldBeEmpty();
	}

	/// <summary>
	/// The decorator base's GetService cannot be overridden, which is the structural guard its own
	/// documentation calls "sealed": a derived decorator cannot reopen unconditional forwarding, only hide
	/// the method with <c>new</c>, which does not change interface dispatch.
	/// <para>
	/// The property that carries this is <see cref="MethodBase.IsFinal"/>, NOT <c>!IsVirtual</c>. A method
	/// implementing an interface member must be virtual in metadata, so the compiler emits even a plainly
	/// declared <c>public object? GetService(Type)</c> as virtual-and-final: measured here,
	/// <c>IsVirtual == true</c> and <c>IsFinal == true</c>. Asserting <c>!IsVirtual</c> reads like the right
	/// check and fails against correct code. The C# <c>sealed</c> keyword is not available either -- it is
	/// legal only on an override -- so <c>IsFinal</c> is the only expression of this guarantee.
	/// </para>
	/// </summary>
	[Fact]
	public void KeepTheDecoratorBaseCapabilityResolutionNonOverridable()
	{
		var method = typeof(IsolatingProjectionStoreDecorator<object>)
			.GetMethod(nameof(IServiceProvider.GetService), Members, null, [typeof(Type)], null);

		method.ShouldNotBeNull();
		method.IsFinal.ShouldBeTrue(
			"a derived decorator must not be able to reopen unconditional capability forwarding");
	}

	// NON-VACUITY, FAILURE MODE 2 of 2 -- THE ASSERTION MUST REDDEN ON A REAL VIOLATION.
	// These fixtures live in the TEST assembly and reproduce the production shape exactly, including the
	// store interface's own default implementation of GetService, so "a decorator that omits GetService"
	// is a reachable state here. No file under src/ is mutated to obtain the red.
	[Theory]
	[InlineData(typeof(MissingGetService<>), true)]        // the defect itself: wraps a store, inherits the default
	[InlineData(typeof(ResolverDecorator<>), true)]        // the TenantRouting shape: a resolver, not a direct store
	[InlineData(typeof(FactoryDecorator<>), true)]
	[InlineData(typeof(LazyDecorator<>), true)]
	[InlineData(typeof(RecordDecorator<>), true)]
	[InlineData(typeof(FieldOnlyDecorator<>), true)]
	[InlineData(typeof(DeclaresGetService<>), false)]
	[InlineData(typeof(ExplicitGetService<>), false)]      // explicit interface impl: private, dotted name
	[InlineData(typeof(InheritsDeclaringBase<>), false)]   // the TenantScoped shape: declared on a base class
	[InlineData(typeof(TerminalStore<>), false)]           // not a decorator; the default is correct for it
	[InlineData(typeof(Unrelated<>), false)]               // holds a store but is not one
	public void ClassifyCompiledTypesWithoutDependingOnSourceSpelling(Type candidate, bool violation)
	{
		// A known-good decorator keeps the population nonempty, so a clean verdict cannot come from refusal.
		Type[] population = [candidate, typeof(DeclaresGetService<>)];

		var violations = FindViolations(population, typeof(IStore<>));

		violations.Contains(candidate).ShouldBe(violation);
		violations.Count.ShouldBe(violation ? 1 : 0);
	}

	[Fact]
	public void RefuseAnEmptyDecoratorPopulation()
	{
		_ = Should.Throw<InvalidOperationException>(() => FindViolations([], typeof(IStore<>)));
		_ = Should.Throw<InvalidOperationException>(() => FindViolations([typeof(TerminalStore<>)], typeof(IStore<>)));
	}

	private static List<Type> FindViolations(IEnumerable<Type> types, Type store)
	{
		var decorators = FindDecorators(types, store);
		if (decorators.Count == 0)
		{
			throw new InvalidOperationException("No projection-store decorators were found in the compiled population.");
		}

		return decorators.Where(t => !DeclaresGetServiceSomewhere(t)).ToList();
	}

	private static List<Type> FindDecorators(IEnumerable<Type> types, Type store) =>
		types.Distinct()
			.Where(t => !t.IsInterface && Implements(t, store) && WrapsStore(t, store) && !IsExemptCapabilityView(t))
			.ToList();

	private static bool IsExemptCapabilityView(Type type)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			// Both spellings: a view named directly, and a view inheriting an exempt base. A type nested in
			// a generic type is itself generic, so its definition is compared as well as the type.
			var definition = current.IsGenericType ? current.GetGenericTypeDefinition() : current;
			if (ExemptCapabilityViews.Contains(definition.FullName ?? "\0", StringComparer.Ordinal) ||
				ExemptCapabilityViews.Contains(current.FullName ?? "\0", StringComparer.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// A default interface implementation is NOT a member of the implementing type, so a type that
	/// inherits one has no GetService among its own declared members and is reported as inheriting. An
	/// explicit interface implementation IS declared, but it is private and carries a dotted name, so it
	/// needs <see cref="BindingFlags.NonPublic"/> and a suffix match rather than name equality.
	/// </summary>
	private static bool DeclaresGetServiceSomewhere(Type type)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (current.GetMethods(Members).Any(m =>
					m.GetParameters() is [{ ParameterType: var parameter }] && parameter == typeof(Type) &&
					(m.Name.Equals(nameof(IServiceProvider.GetService), StringComparison.Ordinal) ||
						m.Name.EndsWith("." + nameof(IServiceProvider.GetService), StringComparison.Ordinal))))
			{
				return true;
			}
		}

		return false;
	}

	private static bool WrapsStore(Type type, Type store)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			// A record's generated copy constructor receives the type itself, not an inner store.
			if (current.GetConstructors(Members).Where(c => !(c.IsDefined(typeof(CompilerGeneratedAttribute)) &&
					c.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == current))
					.SelectMany(c => c.GetParameters()).Any(p => ContainsStore(p.ParameterType, store, type)) ||
				current.GetFields(Members).Any(f => ContainsStore(f.FieldType, store, type)) ||
				current.GetProperties(Members).Any(p => ContainsStore(p.PropertyType, store, type)))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Whether <paramref name="type"/> yields a projection store OTHER THAN <paramref name="self"/>.
	/// <para>
	/// The self-exclusion is load-bearing and was added after measuring. Every provider store declares an
	/// <c>ILogger&lt;TheStoreItself&gt;</c>, and the generic-argument recursion that exists to catch a
	/// resolver or factory yielding a store matched that logger's argument -- so all eight terminal provider
	/// stores were classified as decorators. They happened to pass, which is the dangerous half: the
	/// over-match was invisible while green, and the first terminal store to rely on the interface default
	/// (which is CORRECT for a terminal store) would have been reported as a violation.
	/// </para>
	/// <para>
	/// COST OF THE EXCLUSION, stated: a decorator that obtains an inner store typed as its own class, rather
	/// than as the interface, is no longer matched. No shipped decorator does this -- all four take the
	/// interface or a resolver of it.
	/// </para>
	/// </summary>
	private static bool ContainsStore(Type type, Type store, Type self) =>
		(Implements(type, store) && !IsSelfOrBase(type, self)) ||
		(type.HasElementType && ContainsStore(type.GetElementType()!, store, self)) ||
		(type.IsGenericType && type.GetGenericArguments().Any(argument => ContainsStore(argument, store, self)));

	private static bool IsSelfOrBase(Type candidate, Type self)
	{
		var definition = candidate.IsGenericType ? candidate.GetGenericTypeDefinition() : candidate;
		for (var current = self; current is not null; current = current.BaseType)
		{
			if ((current.IsGenericType ? current.GetGenericTypeDefinition() : current) == definition)
			{
				return true;
			}
		}

		return false;
	}

	private static bool Implements(Type type, Type store) =>
		(type.IsGenericType && type.GetGenericTypeDefinition() == store) ||
		type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == store);

	// A test-local mirror of the production shape: a store interface whose capability lookup is supplied
	// as a default interface implementation answering for the instance itself.
	private interface IStore<T> : IServiceProvider
		where T : class
	{
		object? IServiceProvider.GetService(Type serviceType) =>
			serviceType.IsInstanceOfType(this) ? this : null;
	}

	private interface IResolver<out T>
	{
		T Resolve(string key);
	}

	private sealed class TerminalStore<T> : IStore<T>
		where T : class;

	private sealed class MissingGetService<T>(IStore<T> inner) : IStore<T>
		where T : class
	{
		public IStore<T> Inner { get; } = inner;
	}

	private sealed class FieldOnlyDecorator<T> : IStore<T>
		where T : class
	{
		// A private field with no corresponding constructor parameter also counts as obtaining a store.
		private readonly IStore<T> _inner = new TerminalStore<T>();

		public override string ToString() => _inner.ToString()!;
	}

	private sealed class ResolverDecorator<T>(IResolver<IStore<T>> resolver) : IStore<T>
		where T : class
	{
		public IResolver<IStore<T>> Resolver { get; } = resolver;
	}

	private sealed class FactoryDecorator<T>(Func<IStore<T>> factory) : IStore<T>
		where T : class
	{
		public Func<IStore<T>> Factory { get; } = factory;
	}

	private sealed class LazyDecorator<T>(Lazy<IStore<T>> inner) : IStore<T>
		where T : class
	{
		public Lazy<IStore<T>> Inner { get; } = inner;
	}

	private sealed record RecordDecorator<T>(IStore<T> Inner) : IStore<T>
		where T : class;

	private class DeclaresGetService<T>(IStore<T> inner) : IStore<T>
		where T : class
	{
		protected IStore<T> Inner { get; } = inner;

		public object? GetService(Type serviceType) => Inner.GetService(serviceType);
	}

	private sealed class ExplicitGetService<T>(IStore<T> inner) : IStore<T>
		where T : class
	{
		object? IServiceProvider.GetService(Type serviceType) => inner.GetService(serviceType);
	}

	private sealed class InheritsDeclaringBase<T>() : DeclaresGetService<T>(new TerminalStore<T>())
		where T : class;

	// Mentions of IStore, or an unrelated class holding a store, must not make that class a decorator.
	private sealed class Unrelated<T>
		where T : class
	{
		public IStore<T> Inner { get; } = new TerminalStore<T>();
	}
}
