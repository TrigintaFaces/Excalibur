// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Saga;

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Saga;

/// <summary>
/// Unit tests for <see cref="SagaMetadataGenerator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaMetadataGeneratorShould
{
	[Fact]
	public void ImplementIIncrementalGenerator()
	{
		typeof(SagaMetadataGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void HaveGeneratorAttribute()
	{
		var attributes = typeof(SagaMetadataGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeInstantiable()
	{
		var generator = new SagaMetadataGenerator();
		generator.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(SagaMetadataGenerator).IsSealed.ShouldBeTrue();
	}
}
