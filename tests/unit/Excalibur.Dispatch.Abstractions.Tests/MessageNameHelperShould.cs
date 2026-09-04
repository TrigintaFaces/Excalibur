// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests;

/// <summary>
/// Unit tests for <see cref="MessageNameHelper"/>.
/// </summary>
/// <remarks>
/// The helper used to DERIVE a name from the type (its assembly-qualified name). That name changed
/// whenever the type's namespace, assembly or assembly version changed, so moving a type made every
/// event already stored under the old name unreadable. The name is now DECLARED, and a type that
/// declares none has no name at all -- there is no fallback to fall back to.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventType")]
[Trait("Priority", "0")]
public sealed class MessageNameHelperShould
{
	private const string DeclaredName = "Excalibur.Test.MessageNameHelper.Declared";
	private const string AliasName = "Excalibur.Test.MessageNameHelper.Older";

	#region GetName

	[Fact]
	public void GetName_WithNull_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => MessageNameHelper.GetName(null!));

	[Fact]
	public void GetName_WithDeclaredName_ReturnsTheDeclaredName() =>
		MessageNameHelper.GetName(typeof(DeclaredEvent)).ShouldBe(DeclaredName);

	[Fact]
	public void GetName_DoesNotLeakWhereTheTypeLives()
	{
		// The whole point of declaring: nothing about the type's location reaches the stored name,
		// so moving or renaming the type cannot orphan what it already wrote.
		var name = MessageNameHelper.GetName(typeof(DeclaredEvent));

		name.ShouldNotContain(nameof(DeclaredEvent));
		name.ShouldNotContain(typeof(DeclaredEvent).Namespace!);
		name.ShouldNotContain("Version=");
		name.ShouldNotContain("Culture=");
	}

	[Fact]
	public void GetName_WithNoDeclaredName_Throws()
	{
		// There is deliberately NO derived fallback. Failing here is a configuration error caught at
		// registration; succeeding would mint an unstable identity and write it to durable storage.
		var exception = Should.Throw<InvalidOperationException>(
			() => MessageNameHelper.GetName(typeof(UndeclaredEvent)));

		exception.Message.ShouldContain(nameof(UndeclaredEvent));
		exception.Message.ShouldContain("MessageName");
	}

	[Theory]
	[InlineData(typeof(string))]
	[InlineData(typeof(int))]
	[InlineData(typeof(int[]))]
	[InlineData(typeof(IDisposable))]
	[InlineData(typeof(List<string>))]
	[InlineData(typeof(List<>))]
	public void GetName_WithAnyUndeclaredType_Throws(Type type) =>
		Should.Throw<InvalidOperationException>(() => MessageNameHelper.GetName(type));

	[Fact]
	public void GetName_DoesNotInheritABaseTypesName()
	{
		// A name identifies exactly one type. If a derived type inherited its base's name, two types
		// would claim one name and stored data could not tell them apart when read back.
		Should.Throw<InvalidOperationException>(
			() => MessageNameHelper.GetName(typeof(DerivedFromDeclaredEvent)));
	}

	#endregion

	#region GetDeclaredName

	[Fact]
	public void GetDeclaredName_WithNull_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => MessageNameHelper.GetDeclaredName(null!));

	[Fact]
	public void GetDeclaredName_WithDeclaredName_ReturnsIt() =>
		MessageNameHelper.GetDeclaredName(typeof(DeclaredEvent)).ShouldBe(DeclaredName);

	[Fact]
	public void GetDeclaredName_WithNoDeclaredName_ReturnsNull() =>
		MessageNameHelper.GetDeclaredName(typeof(UndeclaredEvent)).ShouldBeNull();

	#endregion

	#region GetDeclaredAliases

	[Fact]
	public void GetDeclaredAliases_WithNoAliases_IsEmpty() =>
		MessageNameHelper.GetDeclaredAliases(typeof(DeclaredEvent)).ShouldBeEmpty();

	[Fact]
	public void GetDeclaredAliases_ReturnsEveryDeclaredAlias() =>
		MessageNameHelper.GetDeclaredAliases(typeof(AliasedEvent)).ShouldContain(AliasName);

	[Fact]
	public void GetDeclaredAliases_DoesNotChangeThePrimaryName() =>
		// An alias is read-side only: it resolves old stored data, it never becomes what is written.
		MessageNameHelper.GetName(typeof(AliasedEvent)).ShouldNotBe(AliasName);

	#endregion

	#region Test Helpers

	[MessageName(DeclaredName)]
	private sealed record DeclaredEvent;

	private sealed record UndeclaredEvent;

	[MessageName("Excalibur.Test.MessageNameHelper.Base")]
	private record BaseDeclaredEvent;

	private sealed record DerivedFromDeclaredEvent : BaseDeclaredEvent;

	[MessageName("Excalibur.Test.MessageNameHelper.Aliased")]
	[MessageNameAlias(AliasName)]
	private sealed record AliasedEvent;

	#endregion
}
