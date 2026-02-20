// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="HandlerInvokerSourceGenerator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerInvokerSourceGeneratorShould
{
	[Fact]
	public void ImplementIIncrementalGenerator()
	{
		typeof(HandlerInvokerSourceGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void HaveGeneratorAttribute()
	{
		var attributes = typeof(HandlerInvokerSourceGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeInstantiable()
	{
		var generator = new HandlerInvokerSourceGenerator();
		generator.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(HandlerInvokerSourceGenerator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void HaveStaticHandlerInterfacesField()
	{
		// HandlerInvokerSourceGenerator defines a static HandlerInterfaces HashSet
		var field = typeof(HandlerInvokerSourceGenerator)
			.GetField("HandlerInterfaces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		field.ShouldNotBeNull();
	}
}
