// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="CachePolicySourceGenerator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CachePolicySourceGeneratorShould
{
	[Fact]
	public void ImplementIIncrementalGenerator()
	{
		typeof(CachePolicySourceGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void HaveGeneratorAttribute()
	{
		var attributes = typeof(CachePolicySourceGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeInstantiable()
	{
		var generator = new CachePolicySourceGenerator();
		generator.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(CachePolicySourceGenerator).IsSealed.ShouldBeTrue();
	}
}
