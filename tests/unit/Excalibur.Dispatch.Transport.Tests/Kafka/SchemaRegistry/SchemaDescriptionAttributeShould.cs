// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="SchemaDescriptionAttribute"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify attribute constructor, property, and usage.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SchemaDescriptionAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsDescription()
	{
		// Arrange
		const string description = "The unique identifier for this order";

		// Act
		var attribute = new SchemaDescriptionAttribute(description);

		// Assert
		attribute.Description.ShouldBe(description);
	}

	[Fact]
	public void Constructor_ThrowsForNullDescription()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SchemaDescriptionAttribute(null!));
	}

	[Fact]
	public void Constructor_AcceptsEmptyString()
	{
		// Act
		var attribute = new SchemaDescriptionAttribute(string.Empty);

		// Assert
		attribute.Description.ShouldBe(string.Empty);
	}

	#endregion

	#region Attribute Usage Tests

	[Fact]
	public void HasCorrectAttributeUsage()
	{
		// Act
		var usage = (AttributeUsageAttribute?)typeof(SchemaDescriptionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault();

		// Assert
		usage.ShouldNotBeNull();
		usage.ValidOn.ShouldBe(AttributeTargets.Property | AttributeTargets.Field);
	}

	[Fact]
	public void DoesNotAllowMultiple()
	{
		// Act
		var usage = (AttributeUsageAttribute?)typeof(SchemaDescriptionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault();

		// Assert
		usage.ShouldNotBeNull();
		usage.AllowMultiple.ShouldBeFalse();
	}

	[Fact]
	public void IsInherited()
	{
		// Act
		var usage = (AttributeUsageAttribute?)typeof(SchemaDescriptionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault();

		// Assert
		usage.ShouldNotBeNull();
		usage.Inherited.ShouldBeTrue();
	}

	#endregion

	#region Reflection Tests

	[Fact]
	public void CanBeAppliedToProperty()
	{
		// Act
		var property = typeof(TestClass).GetProperty(nameof(TestClass.OrderId));
		var attribute = property.GetCustomAttributes(typeof(SchemaDescriptionAttribute), false)
			.FirstOrDefault() as SchemaDescriptionAttribute;

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Description.ShouldBe("The unique order identifier");
	}

	[Fact]
	public void CanBeAppliedToField()
	{
		// Act
		var field = typeof(TestClass).GetField(nameof(TestClass.TotalAmount));
		var attribute = field.GetCustomAttributes(typeof(SchemaDescriptionAttribute), false)
			.FirstOrDefault() as SchemaDescriptionAttribute;

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Description.ShouldBe("The total order amount");
	}

	#endregion

	#region Class Tests

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(SchemaDescriptionAttribute).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void Class_InheritsFromAttribute()
	{
		// Assert
		typeof(SchemaDescriptionAttribute).IsSubclassOf(typeof(Attribute)).ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private sealed class TestClass
	{
		[SchemaDescription("The unique order identifier")]
		public string? OrderId { get; set; }

		[SchemaDescription("The total order amount")]
		public decimal TotalAmount = default;
	}

	#endregion
}
