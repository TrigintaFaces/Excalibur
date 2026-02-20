// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Abstractions.Tests.Delivery;

/// <summary>
/// Unit tests for <see cref="ExcludeKindsAttribute"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Delivery")]
[Trait("Priority", "0")]
public sealed class ExcludeKindsAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsExcludedKinds()
	{
		// Act
		var attribute = new ExcludeKindsAttribute(MessageKinds.Event);

		// Assert
		attribute.ExcludedKinds.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void Constructor_WithMultipleKinds_SetsAllKinds()
	{
		// Act
		var attribute = new ExcludeKindsAttribute(MessageKinds.Event | MessageKinds.Action);

		// Assert
		attribute.ExcludedKinds.HasFlag(MessageKinds.Event).ShouldBeTrue();
		attribute.ExcludedKinds.HasFlag(MessageKinds.Action).ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithNone_SetsNone()
	{
		// Act
		var attribute = new ExcludeKindsAttribute(MessageKinds.None);

		// Assert
		attribute.ExcludedKinds.ShouldBe(MessageKinds.None);
	}

	#endregion

	#region AttributeUsage Tests

	[Fact]
	public void HasAttributeUsageAttribute()
	{
		// Arrange
		var attributeUsage = typeof(ExcludeKindsAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
	}

	[Fact]
	public void IsApplicableToClassesOnly()
	{
		// Arrange
		var attributeUsage = typeof(ExcludeKindsAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	#endregion

	#region Attribute Application Tests

	[ExcludeKinds(MessageKinds.Event)]
	private sealed class TestClassWithEventExcluded;

	[ExcludeKinds(MessageKinds.Action | MessageKinds.Document)]
	private sealed class TestClassWithMultipleExcluded;

	[Fact]
	public void CanBeAppliedToClass()
	{
		// Arrange
		var attribute = typeof(TestClassWithEventExcluded)
			.GetCustomAttributes(typeof(ExcludeKindsAttribute), false)
			.Cast<ExcludeKindsAttribute>()
			.FirstOrDefault();

		// Assert
		_ = attribute.ShouldNotBeNull();
		attribute.ExcludedKinds.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void CanBeAppliedWithCombinedKinds()
	{
		// Arrange
		var attribute = typeof(TestClassWithMultipleExcluded)
			.GetCustomAttributes(typeof(ExcludeKindsAttribute), false)
			.Cast<ExcludeKindsAttribute>()
			.FirstOrDefault();

		// Assert
		_ = attribute.ShouldNotBeNull();
		attribute.ExcludedKinds.HasFlag(MessageKinds.Action).ShouldBeTrue();
		attribute.ExcludedKinds.HasFlag(MessageKinds.Document).ShouldBeTrue();
	}

	#endregion

	#region Sealed Class Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(ExcludeKindsAttribute).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromAttribute()
	{
		// Arrange
		var attribute = new ExcludeKindsAttribute(MessageKinds.Event);

		// Assert
		_ = attribute.ShouldBeAssignableTo<Attribute>();
	}

	#endregion
}
