// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for the <see cref="RequiresFeaturesAttribute"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class RequiresFeaturesAttributeShould
{
	[Fact]
	public void Constructor_Should_StoreFeatures()
	{
		// Act
		var attr = new RequiresFeaturesAttribute(DispatchFeatures.Validation, DispatchFeatures.Authorization);

		// Assert
		attr.Features.Count.ShouldBe(2);
		attr.Features.ShouldContain(DispatchFeatures.Validation);
		attr.Features.ShouldContain(DispatchFeatures.Authorization);
	}

	[Fact]
	public void Constructor_Should_HandleEmptyFeatures()
	{
		// Act
		var attr = new RequiresFeaturesAttribute();

		// Assert
		attr.Features.Count.ShouldBe(0);
	}

	[Fact]
	public void Should_BeApplicableOnlyToClasses()
	{
		// Act
		var usage = typeof(RequiresFeaturesAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Class);
	}
}
