// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="HandlerActivationGenerator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerActivationGeneratorShould
{
	[Fact]
	public void ImplementIIncrementalGenerator()
	{
		typeof(HandlerActivationGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void HaveGeneratorAttribute()
	{
		var attributes = typeof(HandlerActivationGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeInstantiable()
	{
		var generator = new HandlerActivationGenerator();
		generator.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(HandlerActivationGenerator).IsSealed.ShouldBeTrue();
	}
}
