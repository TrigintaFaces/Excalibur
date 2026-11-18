// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Locks the derivation of a saga identifier from a business correlation key.
/// </summary>
/// <remarks>
/// <para>
/// Before this, <see cref="ISagaEvent.SagaId"/> was the only thing that located a saga instance, so a
/// publisher had to already hold the identifier — which the first message in a conversation cannot, and a
/// service in another bounded context generally never will.
/// </para>
/// <para>
/// <b>The golden values here were computed independently</b>, from the RFC 9562 version 8 algorithm in a
/// separate implementation, not captured from this code's own output. That is the difference between an
/// arm that proves the derivation is CORRECT and one that only proves it has not changed: a self-captured
/// value pins whatever the implementation does, including a byte-order mistake.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaCorrelationShould
{
	/// <summary>
	/// The derivation matches an independent implementation of the algorithm, byte for byte.
	/// </summary>
	[Fact]
	public void MatchAnIndependentlyComputedDerivation()
	{
		SagaCorrelation.ToSagaId("OrderSaga", "ORDER-1001").ShouldBe(
			Guid.Parse("d78942e7-1813-8939-b17c-230d208af7b3"),
			"this value was computed from the RFC 9562 version 8 algorithm by a separate implementation, so "
			+ "it checks the derivation rather than recording it. It also pins BYTE ORDER: a saga correlated "
			+ "on one machine has to derive the same identifier on another, and Guid's own layout is "
			+ "little-endian for its first three fields, so reading the digest in native order would pass "
			+ "every self-consistency check and still split a saga across platforms.");
	}

	/// <summary>
	/// The same key always derives the same identifier. This is the whole mechanism: it is what lets the
	/// ordinary load-by-identifier path find the instance with no lookup table and no provider support.
	/// </summary>
	[Fact]
	public void DeriveTheSameIdentifierForTheSameKey() =>
		SagaCorrelation.ToSagaId("OrderSaga", "ORDER-1001")
			.ShouldBe(SagaCorrelation.ToSagaId("OrderSaga", "ORDER-1001"));

	/// <summary>
	/// Two different saga types correlating on the same business key must not collide.
	/// </summary>
	[Fact]
	public void ScopeTheKeyByTheSagaType()
	{
		SagaCorrelation.ToSagaId("OrderSaga", "ORDER-1001").ShouldNotBe(
			SagaCorrelation.ToSagaId("OtherSaga", "ORDER-1001"),
			"an order number is a perfectly ordinary key for more than one saga to correlate on. Deriving "
			+ "from the key alone would route a shipping saga's event into a billing saga's instance, and "
			+ "both would appear to be working.");

		SagaCorrelation.ToSagaId("OtherSaga", "ORDER-1001").ShouldBe(
			Guid.Parse("480c591b-74ba-8968-ac5e-5bd45ecea51e"),
			"independently computed, so the scoping is checked rather than merely observed to differ");
	}

	/// <summary>
	/// Moving the boundary between the type and the key must change the identifier.
	/// </summary>
	[Fact]
	public void NotLetTheTypeAndKeyBoundaryBeMoved()
	{
		SagaCorrelation.ToSagaId("ab", "c").ShouldNotBe(
			SagaCorrelation.ToSagaId("a", "bc"),
			"unframed concatenation makes these two the same digest input, so a saga type ending "
			+ "in a character that a key could begin with would silently share identifiers with another "
			+ "type. Explicit byte lengths preserve the boundary even when either input contains a delimiter.");

		SagaCorrelation.ToSagaId("ab", "c").ShouldBe(Guid.Parse("3ad91289-3105-81d7-8a50-4578768831b7"));
		SagaCorrelation.ToSagaId("a", "bc").ShouldBe(Guid.Parse("285fb3c8-294a-89ba-8483-593eb27043e3"));
	}

    [Fact]
    [Trait("Pattern", "Regression")]
    public void FrameLiteralDelimiterAndUnicodeInputsWithoutBoundaryAliases()
    {
        SagaCorrelation.ToSagaId("a\u001fb", "c").ShouldBe(Guid.Parse("91125bad-6e12-8f18-b7c5-03ac4582363a"));
        SagaCorrelation.ToSagaId("a", "b\u001fc").ShouldBe(Guid.Parse("67b1191e-a376-87b7-aa0b-e6daf6ed2a73"));
        SagaCorrelation.ToSagaId("a\u001fb", "c").ShouldNotBe(SagaCorrelation.ToSagaId("a", "b\u001fc"));
        SagaCorrelation.ToSagaId("\u03C0\U0001F69A", "\u00E9\0x").ShouldBe(Guid.Parse("ee33ec56-70f5-8556-9664-98e693ff5339"));
    }

    [Fact]
    public void KeepOrdinalLiteralKeyAndTypeSemanticsWithoutNormalization()
    {
        SagaCorrelation.ToSagaId("\u00E9", "e\u0301").ShouldBe(Guid.Parse("c4d71fa2-ad1f-838e-acd5-cf6bbe6fb82a"));
        SagaCorrelation.ToSagaId("type", "a").ShouldNotBe(SagaCorrelation.ToSagaId("type", "A"));
        SagaCorrelation.ToSagaId("type", " a").ShouldNotBe(SagaCorrelation.ToSagaId("type", "a"));
        SagaCorrelation.ToSagaId("type", "\u00E9").ShouldNotBe(SagaCorrelation.ToSagaId("type", "e\u0301"));
        SagaCorrelation.ToSagaId("type", "\ufffd").ShouldBe(Guid.Parse("744df72f-0ffb-8013-a362-a40f1a3039e7"));
    }

    [Fact]
    public void RefuseMalformedUtf16RatherThanAliasTheReplacementCharacter()
    {
        foreach (var invalid in new[] { new string((char)0xD800, 1), new string((char)0xDC00, 1) })
        {
            Should.Throw<ArgumentException>(() => SagaCorrelation.ToSagaId(invalid, "key")).ParamName.ShouldBe("sagaType");
            Should.Throw<ArgumentException>(() => SagaCorrelation.ToSagaId("type", invalid)).ParamName.ShouldBe("correlationKey");
            Should.Throw<ArgumentException>(() => SagaCorrelation.ToSagaId<GenericScope<int>>(invalid)).ParamName.ShouldBe("correlationKey");
        }
    }

    [Fact]
    public void MatchAVersionFreeStructuralTypedScopeIncludingAllGenericArrayArguments()
    {
        // Independently computed from assembly SIMPLE names and metadata definition names, never
        // assembly-qualified constructed generic names containing runtime/assembly versions.
        SagaCorrelation.ToSagaId<GenericScope<Dictionary<string, int[]>>>("ORDER-1001")
            .ShouldBe(Guid.Parse("16e9b0f7-8265-8fcc-929c-de3dd7cac089"));
        SagaCorrelation.ToSagaId<GenericScope<int>>("key").ShouldNotBe(SagaCorrelation.ToSagaId<GenericScope<string>>("key"));
        SagaCorrelation.ToSagaId<GenericScope<int[]>>("key").ShouldNotBe(SagaCorrelation.ToSagaId<GenericScope<int[,]>>("key"));
        SagaCorrelation.ToSagaId<GenericScope<GenericScope<int>>>("key")
            .ShouldNotBe(SagaCorrelation.ToSagaId<GenericScope<int>>("key"));
        SagaCorrelation.ToSagaId<GenericScope<int>>("key").ShouldBe(SagaCorrelation.ToSagaId<GenericScope<int>>("key"));
    }

    [Fact]
    public void PreserveTheSameFramingForInputsBeyondTheSmallStackBuffer()
    {
        SagaCorrelation.ToSagaId("type", new string('x', 2048)).ShouldBe(Guid.Parse("40f558cf-0570-8845-abd2-5a5e76d7adc5"));
    }

    [Fact]
    public void PreserveNullInputRefusalForExplicitAndTypedScopes()
    {
        Should.Throw<ArgumentException>(() => SagaCorrelation.ToSagaId(null!, "key"));
        Should.Throw<ArgumentException>(() => SagaCorrelation.ToSagaId("type", null!));
        Should.Throw<ArgumentException>(() => SagaCorrelation.ToSagaId<GenericScope<int>>(null!));
    }

    [Fact]
    [Trait("Pattern", "Regression")]
    public void DistinguishAssemblySimpleNamesButExcludeVersionsEvenInsideClosedGenericArguments()
    {
        // Dynamic metadata is a MANAGED test fixture only, not part of the NativeAOT implementation.
        // Existing InternalsVisibleTo permits direct typed identity-helper calls: no private reflection.
        var first = MetadataType("SagaScopeFixture", new Version(1, 2, 3, 4));
        var upgraded = MetadataType("SagaScopeFixture", new Version(9, 8, 7, 6));
        var otherAssembly = MetadataType("OtherSagaScopeFixture", new Version(1, 2, 3, 4));
        first.FullName.ShouldBe(upgraded.FullName); first.FullName.ShouldBe(otherAssembly.FullName);
        var scope = SagaCorrelationTypeIdentity.Create(first);
        scope.ShouldBe(SagaCorrelationTypeIdentity.Create(upgraded));
        scope.ShouldNotBe(SagaCorrelationTypeIdentity.Create(otherAssembly));
        SagaCorrelation.ToSagaId(scope, "KEY").ShouldBe(SagaCorrelation.ToSagaId(SagaCorrelationTypeIdentity.Create(upgraded), "KEY"));
        var firstGeneric = typeof(GenericScope<>).MakeGenericType(first.MakeArrayType());
        var upgradedGeneric = typeof(GenericScope<>).MakeGenericType(upgraded.MakeArrayType());
        SagaCorrelationTypeIdentity.Create(firstGeneric).ShouldBe(SagaCorrelationTypeIdentity.Create(upgradedGeneric));
    }

    [Fact]
    public void PreserveNestedDeclaringTypeAndGenericArgumentStructure()
    {
        SagaCorrelation.ToSagaId<FirstOuter.Same<int>>("KEY").ShouldNotBe(SagaCorrelation.ToSagaId<SecondOuter.Same<int>>("KEY"));
        SagaCorrelation.ToSagaId<FirstOuter.Same<int>>("KEY").ShouldNotBe(SagaCorrelation.ToSagaId<FirstOuter.Same<string>>("KEY"));
    }

    private static Type MetadataType(string simpleName, Version version)
    {
        var assembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new System.Reflection.AssemblyName(simpleName) { Version = version }, System.Reflection.Emit.AssemblyBuilderAccess.RunAndCollect);
        return assembly.DefineDynamicModule("scope").DefineType("Fixture.Shared.OrderSaga", System.Reflection.TypeAttributes.Public)
            .CreateType() ?? throw new InvalidOperationException("Managed metadata fixture could not create its type.");
    }
    private static class FirstOuter { internal sealed class Same<T> { } }
    private static class SecondOuter { internal sealed class Same<T> { } }

    private sealed class GenericScope<T> { }

	/// <summary>
	/// The result is a well-formed UUID: RFC 9562 version 8, standard variant.
	/// </summary>
	[Fact]
	public void SetTheVersionAndVariantBits()
	{
		var bytes = SagaCorrelation.ToSagaId("OrderSaga", "ORDER-1001").ToByteArray(bigEndian: true);

		((bytes[6] & 0xF0) >> 4).ShouldBe(
			8,
			"version 8 is the RFC's provision for an application-defined derivation, which is what this is. "
			+ "Version 5 would be the familiar name-based choice and is specified over SHA-1, which this "
			+ "project does not use.");

		(bytes[8] & 0xC0).ShouldBe(
			0x80,
			"the standard variant. Without these bits the value is 16 bytes that merely look like a UUID, "
			+ "and tooling that parses the version is entitled to reject it.");
	}

	/// <summary>
	/// Neither argument has a meaningful empty value, so both are refused.
	/// </summary>
	/// <param name="sagaType">The saga type argument.</param>
	/// <param name="key">The correlation key argument.</param>
	[Theory]
	[InlineData("", "ORDER-1")]
	[InlineData("   ", "ORDER-1")]
	[InlineData("OrderSaga", "")]
	[InlineData("OrderSaga", "   ")]
	public void RefuseAnEmptyTypeOrKey(string sagaType, string key) =>
		_ = Should.Throw<ArgumentException>(() => SagaCorrelation.ToSagaId(sagaType, key),
			"an empty key routes every message to one instance and an empty type lets unrelated sagas share "
			+ "identifiers. Both are silent in production and neither has a sensible default, so they are "
			+ "refused at the boundary rather than producing a derivable-but-wrong identifier.");
}
