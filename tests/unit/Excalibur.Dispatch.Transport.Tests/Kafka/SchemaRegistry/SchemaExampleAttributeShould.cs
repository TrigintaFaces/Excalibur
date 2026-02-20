// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="SchemaExampleAttribute"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify attribute constructor, property, and usage including multiple examples.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SchemaExampleAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsStringExample()
	{
		// Arrange
		const string example = "ORD-12345";

		// Act
		var attribute = new SchemaExampleAttribute(example);

		// Assert
		attribute.Example.ShouldBe(example);
	}

	[Fact]
	public void Constructor_SetsNumericExample()
	{
		// Arrange
		const int example = 42;

		// Act
		var attribute = new SchemaExampleAttribute(example);

		// Assert
		attribute.Example.ShouldBe(example);
	}

	[Fact]
	public void Constructor_SetsDoubleExample()
	{
		// Arrange
		const double example = 99.99;

		// Act
		var attribute = new SchemaExampleAttribute(example);

		// Assert
		attribute.Example.ShouldBe(example);
	}

	[Fact]
	public void Constructor_AcceptsNullExample()
	{
		// Act
		var attribute = new SchemaExampleAttribute(null!);

		// Assert
		attribute.Example.ShouldBeNull();
	}

	#endregion

	#region Attribute Usage Tests

	[Fact]
	public void HasCorrectAttributeUsage()
	{
		// Act
		var usage = (AttributeUsageAttribute?)typeof(SchemaExampleAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault();

		// Assert
		usage.ShouldNotBeNull();
		usage.ValidOn.ShouldBe(AttributeTargets.Property | AttributeTargets.Field);
	}

	[Fact]
	public void AllowsMultiple()
	{
		// Act
		var usage = (AttributeUsageAttribute?)typeof(SchemaExampleAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault();

		// Assert
		usage.ShouldNotBeNull();
		usage.AllowMultiple.ShouldBeTrue();
	}

	[Fact]
	public void IsInherited()
	{
		// Act
		var usage = (AttributeUsageAttribute?)typeof(SchemaExampleAttribute)
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
		var attribute = property.GetCustomAttributes(typeof(SchemaExampleAttribute), false)
			.FirstOrDefault() as SchemaExampleAttribute;

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Example.ShouldBe("ORD-12345");
	}

	[Fact]
	public void MultipleExamples_CanBeApplied()
	{
		// Act
		var property = typeof(TestClass).GetProperty(nameof(TestClass.Status));
		var attributes = property.GetCustomAttributes(typeof(SchemaExampleAttribute), false)
			.Cast<SchemaExampleAttribute>()
			.ToList();

		// Assert
		attributes.Count.ShouldBe(3);
		attributes.Select(a => a.Example).ShouldContain("pending");
		attributes.Select(a => a.Example).ShouldContain("approved");
		attributes.Select(a => a.Example).ShouldContain("rejected");
	}

	[Fact]
	public void CanBeAppliedToField()
	{
		// Act
		var field = typeof(TestClass).GetField(nameof(TestClass.Amount));
		var attribute = field.GetCustomAttributes(typeof(SchemaExampleAttribute), false)
			.FirstOrDefault() as SchemaExampleAttribute;

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Example.ShouldBe(99.99);
	}

	#endregion

	#region Type Support Tests

	[Theory]
	[InlineData("string value")]
	[InlineData(123)]
	[InlineData(45.67)]
	[InlineData(true)]
	[InlineData(false)]
	public void SupportsVariousTypes(object example)
	{
		// Act
		var attribute = new SchemaExampleAttribute(example);

		// Assert
		attribute.Example.ShouldBe(example);
	}

	#endregion

	#region Class Tests

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(SchemaExampleAttribute).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void Class_InheritsFromAttribute()
	{
		// Assert
		typeof(SchemaExampleAttribute).IsSubclassOf(typeof(Attribute)).ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private sealed class TestClass
	{
		[SchemaExample("ORD-12345")]
		public string? OrderId { get; set; }

		[SchemaExample("pending")]
		[SchemaExample("approved")]
		[SchemaExample("rejected")]
		public string? Status { get; set; }

		[SchemaExample(99.99)]
		public decimal Amount;
	}

	#endregion
}
