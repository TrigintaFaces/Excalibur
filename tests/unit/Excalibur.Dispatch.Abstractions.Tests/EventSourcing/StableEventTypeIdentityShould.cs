// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Locks the stored identity of a message: declared, permanent, and unique.
/// </summary>
/// <remarks>
/// <para>
/// Identity used to be the type's assembly-qualified name, so a namespace move, an assembly move, or a
/// routine version bump each rewrote it and made everything already stored unreadable. It is now
/// declared at the type, where nothing about the code layout can reach it.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the two negatives are the point of the suite --
/// <see cref="RefuseToRegisterAMessageThatDeclaresNoName"/> (no silent fallback survives) and
/// <see cref="RefuseTwoTypesClaimingOneName"/> (the silent-overwrite path that would deserialize one
/// type's events into another). Both fail on any implementation that keeps a derived-name fallback or
/// uses an indexer to populate the name map.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StableEventTypeIdentityShould
{
	private const string DeclaredName = "customer-created";
	private const string RetiredName = "customer-created-v1";

	[MessageName(DeclaredName)]
	[MessageNameAlias(RetiredName)]
	private sealed record DeclaredNameEvent(string Id);

	[MessageName("employee-created")]
	private sealed record OtherDeclaredNameEvent(string Id);

	private sealed record UndeclaredNameEvent(string Id);

	// A type whose SECOND alias collides. Its primary name and first alias are free, so a registry
	// that wrote as it went would bind both before discovering the conflict on the third candidate.
	[MessageName("partial-primary")]
	[MessageNameAlias("partial-spare")]
	[MessageNameAlias("customer-created")]
	private sealed record CollidingOnItsLastAliasEvent(string Id);

	private static IEventTypeRegistry RegistryFor(params Type[] types)
	{
		var services = new ServiceCollection();
		_ = services.AddEventTypes(types);
		return services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>();
	}

	[Fact]
	public void StoreTheDeclaredNameAndNothingDerivedFromTheType()
	{
		var registry = RegistryFor(typeof(DeclaredNameEvent));

		registry.GetTypeName(typeof(DeclaredNameEvent)).ShouldBe(DeclaredName);
		MessageNameHelper.GetName(typeof(DeclaredNameEvent)).ShouldBe(DeclaredName);

		// Nothing about where the type lives reaches the stored name.
		DeclaredName.ShouldNotContain(nameof(DeclaredNameEvent));
		DeclaredName.ShouldNotContain("Version=");
	}

	[Fact]
	public void RefuseToRegisterAMessageThatDeclaresNoName()
	{
		// The absent fallback. Previously this registered silently under the assembly-qualified name,
		// which is the identity that breaks when the type moves or the assembly version changes.
		var exception = Should.Throw<InvalidOperationException>(
			() => RegistryFor(typeof(UndeclaredNameEvent)));

		exception.Message.ShouldContain(nameof(UndeclaredNameEvent));
		exception.Message.ShouldContain("MessageName");
	}

	[Fact]
	public void RefuseTwoTypesClaimingOneName()
	{
		// Stored data records the name and nothing else, so two types sharing one cannot be told apart
		// when read back: the second registration would take ownership and the first type's events
		// would deserialize into the second. Readable, plausible, and wrong.
		var registry = new EventTypeRegistry();
		registry.Register(typeof(DeclaredNameEvent));

		var exception = Should.Throw<InvalidOperationException>(
			() => registry.RegisterAlias(DeclaredName, typeof(OtherDeclaredNameEvent)));

		exception.Message.ShouldContain(DeclaredName);
		exception.Message.ShouldContain(nameof(DeclaredNameEvent));
		exception.Message.ShouldContain(nameof(OtherDeclaredNameEvent));
	}

	[Fact]
	public void WriteAllOfATypesNamesOrNoneOfThem()
	{
		// Registration is all-or-nothing. Binding the canonical name and then throwing on a later alias
		// would leave the type half-registered with no way back -- and a consumer loading plugins inside
		// a try/catch would swallow the throw and keep that partial state, so events would later store
		// under a name the registry only half-knows.
		var registry = new EventTypeRegistry();
		registry.Register(typeof(DeclaredNameEvent));

		_ = Should.Throw<InvalidOperationException>(
			() => registry.Register(typeof(CollidingOnItsLastAliasEvent)));

		// Nothing about the rejected type survives -- not the primary, not the alias that was free.
		registry.GetTypeName(typeof(CollidingOnItsLastAliasEvent))
			.ShouldBeNull("a refused registration must leave no type-to-name binding behind");
		registry.ResolveType("partial-primary")
			.ShouldBeNull("the canonical name must not be bound by a registration that threw");
		registry.ResolveType("partial-spare")
			.ShouldBeNull("an alias earlier in the list than the collision must not be bound either");

		// And the incumbent is untouched.
		registry.ResolveType(RetiredName).ShouldBe(typeof(DeclaredNameEvent));
	}

	[Fact]
	public void BindEveryNameOfATypeThatDoesNotCollide()
	{
		// Liveness arm for WriteAllOfATypesNamesOrNoneOfThem. Without this, that test's "partial-spare
		// resolves to nothing" would also pass if the name were never bindable at all -- a vacuous
		// green. Registered alone, all three names DO bind, so the nulls there are caused by the
		// rollback and by nothing else.
		var registry = new EventTypeRegistry();
		registry.Register(typeof(CollidingOnItsLastAliasEvent));

		registry.GetTypeName(typeof(CollidingOnItsLastAliasEvent)).ShouldBe("partial-primary");
		registry.ResolveType("partial-primary").ShouldBe(typeof(CollidingOnItsLastAliasEvent));
		registry.ResolveType("partial-spare").ShouldBe(typeof(CollidingOnItsLastAliasEvent));
	}

	[Fact]
	public void AcceptTheSameTypeClaimingItsOwnNameTwice()
	{
		// Re-registration is idempotent; only a CONFLICTING claim is refused.
		var registry = new EventTypeRegistry();
		registry.Register(typeof(DeclaredNameEvent));

		Should.NotThrow(() => registry.Register(typeof(DeclaredNameEvent)));
		registry.ResolveType(DeclaredName).ShouldBe(typeof(DeclaredNameEvent));
	}

	[Fact]
	public void ResolveANameTheTypeDeclaresItWasPreviouslyKnownBy()
	{
		// How a consumer renames a message of their own without orphaning what they have stored.
		var registry = RegistryFor(typeof(DeclaredNameEvent));

		registry.ResolveType(RetiredName).ShouldBe(typeof(DeclaredNameEvent));
	}

	[Fact]
	public void NeverWriteAMessageUnderARetiredName()
	{
		// An alias is a reading concession. If it reached the write path the retired name would spread
		// into new data and the store would never converge on the current one.
		var registry = RegistryFor(typeof(DeclaredNameEvent));

		registry.GetTypeName(typeof(DeclaredNameEvent)).ShouldBe(DeclaredName);
		registry.GetTypeName(typeof(DeclaredNameEvent)).ShouldNotBe(RetiredName);
	}

	[Fact]
	[RequiresDynamicCode("Resolution of an assembly-qualified name may touch reflection paths")]
	public void StillRefuseATypeThatWasNeverRegistered()
	{
		// The allow-list is the security control and must be unaffected by any of the above.
		var registry = RegistryFor(typeof(DeclaredNameEvent));
		var serializer = new JsonEventSerializer(registry, options: null, allowAssemblyScan: false);

		registry.ResolveType(typeof(string).AssemblyQualifiedName!).ShouldBeNull();
		_ = Should.Throw<UnknownEventTypeException>(
			() => serializer.ResolveType(typeof(string).AssemblyQualifiedName!));
	}

	[Fact]
	public void RefuseANameThatWouldNeedEscapingWhereItIsStored()
	{
		// The name reaches database columns, URLs, file names and broker topics. Enforced at the
		// declaration rather than described and hoped for.
		_ = Should.Throw<ArgumentException>(() => new MessageNameAttribute("has space"));
		_ = Should.Throw<ArgumentException>(() => new MessageNameAttribute("-leading-separator"));
		_ = Should.Throw<ArgumentException>(() => new MessageNameAttribute("trailing-separator-"));
		_ = Should.Throw<ArgumentException>(() => new MessageNameAttribute(new string('x', 257)));
		_ = Should.Throw<ArgumentException>(() => new MessageNameAttribute("  "));

		// An old assembly-qualified name is not a usable declared name either -- every previous name
		// was itself declared, so it already had this shape.
		_ = Should.Throw<ArgumentException>(
			() => new MessageNameAliasAttribute("Ns.T, Asm, Version=1.0.0.0"));

		Should.NotThrow(() => new MessageNameAttribute("customer-created"));
		Should.NotThrow(() => new MessageNameAttribute("Contoso.Sales:CustomerCreated_v2"));
	}

	[Fact]
	public void ExposeTheDeclaredNameToAnySubsystemWithoutImposingItsFallback()
	{
		// The attribute is the shared source of truth for events and for audited actions. An event
		// store demands a name because an unresolvable event is corruption; an audit trail cannot fail
		// fast -- it has no registration seam -- so it degrades to the type name instead.
		MessageNameHelper.GetDeclaredName(typeof(DeclaredNameEvent)).ShouldBe(DeclaredName);
		MessageNameHelper.GetDeclaredName(typeof(UndeclaredNameEvent)).ShouldBeNull();

		(MessageNameHelper.GetDeclaredName(typeof(UndeclaredNameEvent)) ?? nameof(UndeclaredNameEvent))
			.ShouldBe(nameof(UndeclaredNameEvent));
	}
}
