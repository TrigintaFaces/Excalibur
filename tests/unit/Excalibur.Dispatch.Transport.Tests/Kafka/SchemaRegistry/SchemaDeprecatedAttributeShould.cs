// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="SchemaDeprecatedAttribute"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify attribute constructors, properties, and usage.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SchemaDeprecatedAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_SetsMessageToNull()
	{
		// Act
		var attribute = new SchemaDeprecatedAttribute();

		// Assert
		attribute.Message.ShouldBeNull();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "Use NewProperty instead";

		// Act
		var attribute = new SchemaDeprecatedAttribute(message);

		// Assert
		attribute.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageConstructor_AcceptsEmptyString()
	{
		// Act
		var attribute = new SchemaDeprecatedAttribute(string.Empty);

		// Assert
		attribute.Message.ShouldBe(string.Empty);
	}

	#endregion

	#region Attribute Usage Tests

	[Fact]
	public void HasCorrectAttributeUsage()
	{
		// Act
		var usage = (AttributeUsageAttribute?)typeof(SchemaDeprecatedAttribute)
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
		var usage = (AttributeUsageAttribute?)typeof(SchemaDeprecatedAttribute)
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
		var usage = (AttributeUsageAttribute?)typeof(SchemaDeprecatedAttribute)
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
		var property = typeof(TestClass).GetProperty(nameof(TestClass.DeprecatedProperty));
		var attribute = property.GetCustomAttributes(typeof(SchemaDeprecatedAttribute), false)
			.FirstOrDefault() as SchemaDeprecatedAttribute;

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Message.ShouldBe("Use NewProperty");
	}

	[Fact]
	public void CanBeAppliedToPropertyWithoutMessage()
	{
		// Act
		var property = typeof(TestClass).GetProperty(nameof(TestClass.DeprecatedWithoutMessage));
		var attribute = property.GetCustomAttributes(typeof(SchemaDeprecatedAttribute), false)
			.FirstOrDefault() as SchemaDeprecatedAttribute;

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Message.ShouldBeNull();
	}

	[Fact]
	public void CanBeAppliedToField()
	{
		// Act
		var field = typeof(TestClass).GetField(nameof(TestClass.DeprecatedField));
		var attribute = field.GetCustomAttributes(typeof(SchemaDeprecatedAttribute), false)
			.FirstOrDefault() as SchemaDeprecatedAttribute;

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Message.ShouldBe("Use NewField");
	}

	#endregion

	#region Class Tests

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(SchemaDeprecatedAttribute).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void Class_InheritsFromAttribute()
	{
		// Assert
		typeof(SchemaDeprecatedAttribute).IsSubclassOf(typeof(Attribute)).ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private sealed class TestClass
	{
		[SchemaDeprecated("Use NewProperty")]
		public string? DeprecatedProperty { get; set; }

		[SchemaDeprecated]
		public string? DeprecatedWithoutMessage { get; set; }

		[SchemaDeprecated("Use NewField")]
		public string? DeprecatedField;
	}

	#endregion
}
