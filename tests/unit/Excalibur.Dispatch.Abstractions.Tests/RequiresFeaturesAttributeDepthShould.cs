// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Depth coverage tests for <see cref="RequiresFeaturesAttribute"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RequiresFeaturesAttributeDepthShould
{
	[Fact]
	public void Constructor_WithSingleFeature_StoresFeature()
	{
		// Act
		var attr = new RequiresFeaturesAttribute(DispatchFeatures.Inbox);

		// Assert
		attr.Features.Count.ShouldBe(1);
		attr.Features[0].ShouldBe(DispatchFeatures.Inbox);
	}

	[Fact]
	public void Constructor_WithMultipleFeatures_StoresAllFeatures()
	{
		// Act
		var attr = new RequiresFeaturesAttribute(
			DispatchFeatures.Inbox,
			DispatchFeatures.Outbox,
			DispatchFeatures.Tracing);

		// Assert
		attr.Features.Count.ShouldBe(3);
		attr.Features.ShouldContain(DispatchFeatures.Inbox);
		attr.Features.ShouldContain(DispatchFeatures.Outbox);
		attr.Features.ShouldContain(DispatchFeatures.Tracing);
	}

	[Fact]
	public void Constructor_WithNoFeatures_ReturnsEmptyList()
	{
		// Act
		var attr = new RequiresFeaturesAttribute();

		// Assert
		attr.Features.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_WithNullParams_ReturnsEmptyList()
	{
		// Act
		var attr = new RequiresFeaturesAttribute(null!);

		// Assert
		attr.Features.ShouldBeEmpty();
	}

	[Fact]
	public void Features_IsReadOnly()
	{
		// Arrange
		var attr = new RequiresFeaturesAttribute(DispatchFeatures.Validation);

		// Assert
		attr.Features.ShouldBeAssignableTo<IReadOnlyList<DispatchFeatures>>();
	}

	[Fact]
	public void IsAttribute()
	{
		// Assert
		typeof(RequiresFeaturesAttribute).IsSubclassOf(typeof(Attribute)).ShouldBeTrue();
	}

	[Fact]
	public void AttributeUsage_TargetsClassOnly()
	{
		// Arrange
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(RequiresFeaturesAttribute),
			typeof(AttributeUsageAttribute))!;

		// Assert
		usage.ShouldNotBeNull();
		usage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void Constructor_PreservesOrder()
	{
		// Act
		var attr = new RequiresFeaturesAttribute(
			DispatchFeatures.Authorization,
			DispatchFeatures.Validation,
			DispatchFeatures.Inbox);

		// Assert — order should be preserved
		attr.Features[0].ShouldBe(DispatchFeatures.Authorization);
		attr.Features[1].ShouldBe(DispatchFeatures.Validation);
		attr.Features[2].ShouldBe(DispatchFeatures.Inbox);
	}

	[Fact]
	public void Constructor_AllowsDuplicateFeatures()
	{
		// Act
		var attr = new RequiresFeaturesAttribute(
			DispatchFeatures.Inbox,
			DispatchFeatures.Inbox);

		// Assert — duplicates are stored as-is
		attr.Features.Count.ShouldBe(2);
	}

	[Fact]
	public void Constructor_WithAllFeatures_StoresAll()
	{
		// Act
		var attr = new RequiresFeaturesAttribute(
			DispatchFeatures.Inbox,
			DispatchFeatures.Outbox,
			DispatchFeatures.Tracing,
			DispatchFeatures.Metrics,
			DispatchFeatures.Validation,
			DispatchFeatures.Authorization,
			DispatchFeatures.Transactions);

		// Assert
		attr.Features.Count.ShouldBe(7);
	}
}
