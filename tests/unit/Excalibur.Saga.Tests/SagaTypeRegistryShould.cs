// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;

namespace Excalibur.Saga.Tests;

/// <summary>
/// Tests for <see cref="SagaTypeRegistry"/> covering type registration, resolution by
/// full name / assembly-qualified name / simple name, and edge cases.
/// Sprint 737 T.21: Wave 3 AOT tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "AOT")]
public sealed class SagaTypeRegistryShould
{
	[Fact]
	public void ResolveTypeByFullName()
	{
		var sut = new SagaTypeRegistry();
		sut.RegisterType(typeof(TestSagaTimeout));

		var resolved = sut.ResolveType(typeof(TestSagaTimeout).FullName!);

		resolved.ShouldBe(typeof(TestSagaTimeout));
	}

	[Fact]
	public void ResolveTypeByAssemblyQualifiedName()
	{
		var sut = new SagaTypeRegistry();
		sut.RegisterType(typeof(TestSagaTimeout));

		var resolved = sut.ResolveType(typeof(TestSagaTimeout).AssemblyQualifiedName!);

		resolved.ShouldBe(typeof(TestSagaTimeout));
	}

	[Fact]
	public void ResolveTypeBySimpleNameFromAssemblyQualifiedInput()
	{
		var sut = new SagaTypeRegistry();
		sut.RegisterType(typeof(TestSagaTimeout));

		// Pass an assembly-qualified name where only the simple part matches
		// The registry tries stripping the assembly suffix to match
		var fullName = typeof(TestSagaTimeout).FullName!;
		var fakeAqn = $"{fullName}, SomeOtherAssembly, Version=1.0.0.0";

		var resolved = sut.ResolveType(fakeAqn);

		resolved.ShouldBe(typeof(TestSagaTimeout));
	}

	[Fact]
	public void ReturnNullForUnregisteredType()
	{
		var sut = new SagaTypeRegistry();

		var resolved = sut.ResolveType("NonExistent.Type.Name");

		resolved.ShouldBeNull();
	}

	[Fact]
	public void ReturnNullForEmptyTypeName()
	{
		var sut = new SagaTypeRegistry();
		sut.RegisterType(typeof(TestSagaTimeout));

		// Empty string has no comma -> no simple name fallback
		var resolved = sut.ResolveType(string.Empty);

		resolved.ShouldBeNull();
	}

	[Fact]
	public void ThrowForNullTypeOnRegister()
	{
		var sut = new SagaTypeRegistry();

		Should.Throw<ArgumentNullException>(() => sut.RegisterType(null!));
	}

	[Fact]
	public void RegisterMultipleTypes()
	{
		var sut = new SagaTypeRegistry();
		sut.RegisterType(typeof(TestSagaTimeout));
		sut.RegisterType(typeof(AnotherSagaTimeout));

		sut.ResolveType(typeof(TestSagaTimeout).FullName!).ShouldBe(typeof(TestSagaTimeout));
		sut.ResolveType(typeof(AnotherSagaTimeout).FullName!).ShouldBe(typeof(AnotherSagaTimeout));
	}

	[Fact]
	public void OverwriteOnDuplicateRegistration()
	{
		var sut = new SagaTypeRegistry();

		// Register twice -- should not throw, just overwrite
		sut.RegisterType(typeof(TestSagaTimeout));
		sut.RegisterType(typeof(TestSagaTimeout));

		sut.ResolveType(typeof(TestSagaTimeout).FullName!).ShouldBe(typeof(TestSagaTimeout));
	}

	[Fact]
	public void ThrowWhenRegisteringAfterFreeze()
	{
		var sut = new SagaTypeRegistry();
		sut.RegisterType(typeof(TestSagaTimeout));
		sut.Freeze();

		Should.Throw<InvalidOperationException>(() => sut.RegisterType(typeof(AnotherSagaTimeout)));
	}

	[Fact]
	public void AllowResolutionAfterFreeze()
	{
		var sut = new SagaTypeRegistry();
		sut.RegisterType(typeof(TestSagaTimeout));
		sut.Freeze();

		sut.ResolveType(typeof(TestSagaTimeout).FullName!).ShouldBe(typeof(TestSagaTimeout));
	}

	[Fact]
	public void AllowDoubleFreezeWithoutThrow()
	{
		var sut = new SagaTypeRegistry();
		sut.Freeze();
		sut.Freeze(); // Should not throw
	}

	// --- Test helpers ---

	internal sealed class TestSagaTimeout
	{
		public string SagaId { get; set; } = string.Empty;
		public DateTimeOffset ScheduledAt { get; set; }
	}

	internal sealed class AnotherSagaTimeout
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
