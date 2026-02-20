// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Validation;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="AotValidatableAttribute"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AotValidatableAttributeShould
{
	[Fact]
	public void BeInstantiable()
	{
		var attribute = new AotValidatableAttribute();
		attribute.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(AotValidatableAttribute).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void InheritFromAttribute()
	{
		typeof(AotValidatableAttribute).BaseType.ShouldBe(typeof(Attribute));
	}

	[Fact]
	public void HaveAttributeUsageForClassOnly()
	{
		var usage = typeof(AotValidatableAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		usage.ValidOn.ShouldBe(AttributeTargets.Class);
		usage.Inherited.ShouldBeFalse();
		usage.AllowMultiple.ShouldBeFalse();
	}

	[Fact]
	public void DefaultIncludeCrossPropertyValidationToTrue()
	{
		var attribute = new AotValidatableAttribute();
		attribute.IncludeCrossPropertyValidation.ShouldBeTrue();
	}

	[Fact]
	public void DefaultValidatorClassNameToNull()
	{
		var attribute = new AotValidatableAttribute();
		attribute.ValidatorClassName.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingIncludeCrossPropertyValidation()
	{
		var attribute = new AotValidatableAttribute
		{
			IncludeCrossPropertyValidation = false,
		};
		attribute.IncludeCrossPropertyValidation.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingValidatorClassName()
	{
		var attribute = new AotValidatableAttribute
		{
			ValidatorClassName = "MyCustomValidator",
		};
		attribute.ValidatorClassName.ShouldBe("MyCustomValidator");
	}
}
