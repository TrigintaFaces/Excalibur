// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests;

/// <summary>
/// net10-valid, non-vacuous behavioural lock for <c>bd-ib1kxp</c> / <c>fcdn6k</c> on the keyed-safe
/// <see cref="ServiceDescriptorExtensions"/> accessors.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this lock exists (and why the original AC-1/2/3 decorator lock is vacuous on net10):</b> the
/// historical bug (<c>bd-ib1kxp</c>) was that the keyed branch of <c>CreateInnerMessageBus</c> read the
/// <em>non-keyed</em> <see cref="ServiceDescriptor.ImplementationInstance"/> getter on a <em>keyed</em>
/// descriptor. On .NET 8.x that getter <em>throws</em> <see cref="System.InvalidOperationException"/>
/// (dotnet/runtime#95789), which is observable. On <b>net10 — the only ship target</b> — that same raw
/// getter <b>returns <see langword="null"/> silently</b> instead of throwing, and the decorator's keyed
/// fallbacks make the silent skip benign, so the historical bug does <b>not</b> reproduce there and a
/// decorator-level test cannot tell the fixed code from the pre-fix code (4/4 GREEN on a realistic pre-fix
/// mutant — vacuous).
/// </para>
/// <para>
/// The invariant that <em>is</em> net10-observable lives in the accessor itself: for a keyed descriptor the
/// keyed-safe accessor MUST return the <em>keyed</em> member, whereas the raw non-keyed getter returns
/// <see langword="null"/>. Dropping the <c>IsKeyedService ? keyed : non-keyed</c> branch (the one-token
/// mutant that re-introduces the bug) makes each accessor return <see langword="null"/> on net10 → these
/// assertions go RED. This is the structural counterpart to the syntax guard
/// <c>KeyedServiceDescriptorAccessGuardTests</c> (which forbids the raw read in shipped source): the guard
/// proves the raw read is absent; this lock proves the sanctioned accessor reads the keyed member.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Abstractions")]
public sealed class ServiceDescriptorExtensionsKeyedSafetyShould : UnitTestBase
{
	private interface IWidget;

	private sealed class Widget : IWidget;

	private const string Key = "widget-key";

	// ── Keyed descriptors: the accessor MUST read the KEYED member (net10-observable, non-vacuous) ──

	[Fact]
	public void GetImplementationInstance_ForKeyedDescriptor_ReturnsKeyedInstance_NotTheNullRawGetter()
	{
		// Arrange: a keyed descriptor registered with a pre-built instance.
		var instance = new Widget();
		var descriptor = KeyedDescriptor(sc => sc.AddKeyedSingleton<IWidget>(Key, instance));

		// Guard the net10 premise: the RAW non-keyed getter returns null (it does NOT throw on net10).
		// On net10 this is what the pre-fix code silently read — proving the accessor's keyed branch is
		// the only thing that recovers the real instance here.
		descriptor.ImplementationInstance.ShouldBeNull(
			"net10 premise: the raw non-keyed ImplementationInstance getter returns null for a keyed descriptor.");

		// Act
		var resolved = descriptor.GetImplementationInstance();

		// Assert: the keyed-safe accessor recovers the keyed instance the raw getter could not.
		resolved.ShouldBeSameAs(instance);
	}

	[Fact]
	public void GetImplementationType_ForKeyedDescriptor_ReturnsKeyedType_NotTheNullRawGetter()
	{
		// Arrange: a keyed descriptor registered by implementation TYPE.
		var descriptor = KeyedDescriptor(sc => sc.AddKeyedSingleton<IWidget, Widget>(Key));

		// net10 premise: the raw non-keyed type getter returns null for a keyed descriptor.
		descriptor.ImplementationType.ShouldBeNull(
			"net10 premise: the raw non-keyed ImplementationType getter returns null for a keyed descriptor.");

		// Act
		var resolved = descriptor.GetImplementationType();

		// Assert
		resolved.ShouldBe(typeof(Widget));
	}

	[Fact]
	public void GetImplementationFactory_ForKeyedDescriptor_ReturnsKeyForwardingFactory_NotTheNullRawGetter()
	{
		// Arrange: a keyed descriptor registered by keyed FACTORY.
		var produced = new Widget();
		var descriptor = KeyedDescriptor(sc =>
			sc.AddKeyedSingleton<IWidget>(Key, (_, key) =>
			{
				// The accessor must forward the descriptor's ServiceKey to the underlying keyed factory.
				key.ShouldBe(Key);
				return produced;
			}));

		// net10 premise: the raw non-keyed factory getter returns null for a keyed descriptor.
		descriptor.ImplementationFactory.ShouldBeNull(
			"net10 premise: the raw non-keyed ImplementationFactory getter returns null for a keyed descriptor.");

		// Act
		var factory = descriptor.GetImplementationFactory();

		// Assert: a usable, key-forwarding factory (callable with the provider alone) is returned.
		factory.ShouldNotBeNull();
		using var provider = new ServiceCollection().BuildServiceProvider();
		factory(provider).ShouldBeSameAs(produced);
	}

	// ── Non-keyed descriptors: the accessor MUST pass through the non-keyed member (no regression) ──

	[Fact]
	public void GetImplementationInstance_ForNonKeyedDescriptor_ReturnsNonKeyedInstance()
	{
		var instance = new Widget();
		var descriptor = SingleDescriptor(sc => sc.AddSingleton<IWidget>(instance));

		descriptor.GetImplementationInstance().ShouldBeSameAs(instance);
	}

	[Fact]
	public void GetImplementationType_ForNonKeyedDescriptor_ReturnsNonKeyedType()
	{
		var descriptor = SingleDescriptor(sc => sc.AddSingleton<IWidget, Widget>());

		descriptor.GetImplementationType().ShouldBe(typeof(Widget));
	}

	[Fact]
	public void GetImplementationFactory_ForNonKeyedDescriptor_ReturnsNonKeyedFactory()
	{
		var produced = new Widget();
		var descriptor = SingleDescriptor(sc => sc.AddSingleton<IWidget>(_ => produced));

		var factory = descriptor.GetImplementationFactory();

		factory.ShouldNotBeNull();
		using var provider = new ServiceCollection().BuildServiceProvider();
		factory(provider).ShouldBeSameAs(produced);
	}

	// ── Null-guard (failure-path coverage) ──

	[Fact]
	public void GetImplementationAccessors_ThrowArgumentNull_ForNullDescriptor()
	{
		ServiceDescriptor descriptor = null!;

		Should.Throw<ArgumentNullException>(() => descriptor.GetImplementationInstance());
		Should.Throw<ArgumentNullException>(() => descriptor.GetImplementationType());
		Should.Throw<ArgumentNullException>(() => descriptor.GetImplementationFactory());
	}

	private static ServiceDescriptor KeyedDescriptor(Action<ServiceCollection> register)
	{
		var descriptor = SingleDescriptor(register);
		descriptor.IsKeyedService.ShouldBeTrue("test setup expected a keyed descriptor.");
		return descriptor;
	}

	private static ServiceDescriptor SingleDescriptor(Action<ServiceCollection> register)
	{
		var services = new ServiceCollection();
		register(services);
		return services.ShouldHaveSingleItem();
	}
}
