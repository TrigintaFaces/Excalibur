// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Validation;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FieldConstraintsShould
{
	// Test message with various typed properties
	private sealed class TestMessage : IDispatchMessage
	{
		public string Name { get; set; } = "test";
		public Guid Id { get; set; } = Guid.NewGuid();
		public int Count { get; set; } = 5;
		public long LongCount { get; set; } = 10L;
		public decimal Price { get; set; } = 9.99m;
		public double Ratio { get; set; } = 0.5;
		public float Factor { get; set; } = 1.5f;
		public DateTime FutureDate { get; set; } = DateTime.UtcNow.AddDays(1);
		public DateTimeOffset FutureDateOffset { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
	}

	// --- NonEmptyStringConstraint ---

	[Fact]
	public void NonEmptyStringConstraint_WithNonEmptyString_IsSatisfied()
	{
		// Arrange
		var constraint = new NonEmptyStringConstraint("Name", "Name is required");
		var message = new TestMessage { Name = "Hello" };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void NonEmptyStringConstraint_WithEmptyString_IsNotSatisfied()
	{
		// Arrange
		var constraint = new NonEmptyStringConstraint("Name", "Name is required");
		var message = new TestMessage { Name = "" };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void NonEmptyStringConstraint_WithWhitespaceString_IsNotSatisfied()
	{
		// Arrange
		var constraint = new NonEmptyStringConstraint("Name", "Name is required");
		var message = new TestMessage { Name = "   " };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void NonEmptyStringConstraint_WithNonStringField_IsNotSatisfied()
	{
		// Arrange
		var constraint = new NonEmptyStringConstraint("Count", "Count must be string");
		var message = new TestMessage { Count = 5 };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void NonEmptyStringConstraint_WithNonExistentField_IsNotSatisfied()
	{
		// Arrange
		var constraint = new NonEmptyStringConstraint("NonExistent", "Field missing");
		var message = new TestMessage();

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void NonEmptyStringConstraint_Properties_AreSet()
	{
		// Act
		var constraint = new NonEmptyStringConstraint("Name", "Name is required");

		// Assert
		constraint.FieldName.ShouldBe("Name");
		constraint.ErrorMessage.ShouldBe("Name is required");
	}

	// --- GuidFormatConstraint ---

	[Fact]
	public void GuidFormatConstraint_WithValidGuid_IsSatisfied()
	{
		// Arrange
		var constraint = new GuidFormatConstraint("Id", "Id must be GUID");
		var message = new TestMessage { Id = Guid.NewGuid() };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void GuidFormatConstraint_WithEmptyGuid_IsNotSatisfied()
	{
		// Arrange
		var constraint = new GuidFormatConstraint("Id", "Id must not be empty");
		var message = new TestMessage { Id = Guid.Empty };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void GuidFormatConstraint_WithValidGuidString_IsSatisfied()
	{
		// Arrange
		var constraint = new GuidFormatConstraint("Name", "Name must be GUID");
		var message = new TestMessage { Name = Guid.NewGuid().ToString() };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void GuidFormatConstraint_WithInvalidGuidString_IsNotSatisfied()
	{
		// Arrange
		var constraint = new GuidFormatConstraint("Name", "Name must be GUID");
		var message = new TestMessage { Name = "not-a-guid" };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void GuidFormatConstraint_WithEmptyGuidString_IsNotSatisfied()
	{
		// Arrange
		var constraint = new GuidFormatConstraint("Name", "Name must be non-empty GUID");
		var message = new TestMessage { Name = Guid.Empty.ToString() };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void GuidFormatConstraint_WithNonGuidType_IsNotSatisfied()
	{
		// Arrange
		var constraint = new GuidFormatConstraint("Count", "Count must be GUID");
		var message = new TestMessage { Count = 5 };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	// --- PositiveNumberConstraint ---

	[Fact]
	public void PositiveNumberConstraint_WithPositiveInt_IsSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Count", "Count must be positive");
		var message = new TestMessage { Count = 5 };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void PositiveNumberConstraint_WithZeroInt_IsSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Count", "Count must be non-negative");
		var message = new TestMessage { Count = 0 };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void PositiveNumberConstraint_WithNegativeInt_IsNotSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Count", "Count must be positive");
		var message = new TestMessage { Count = -1 };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void PositiveNumberConstraint_WithPositiveLong_IsSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("LongCount", "LongCount must be positive");
		var message = new TestMessage { LongCount = 10L };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void PositiveNumberConstraint_WithNegativeLong_IsNotSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("LongCount", "Must be positive");
		var message = new TestMessage { LongCount = -1L };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void PositiveNumberConstraint_WithPositiveDecimal_IsSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Price", "Price must be positive");
		var message = new TestMessage { Price = 9.99m };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void PositiveNumberConstraint_WithNegativeDecimal_IsNotSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Price", "Must be positive");
		var message = new TestMessage { Price = -0.01m };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void PositiveNumberConstraint_WithPositiveDouble_IsSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Ratio", "Ratio must be positive");
		var message = new TestMessage { Ratio = 0.5 };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void PositiveNumberConstraint_WithNegativeDouble_IsNotSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Ratio", "Must be positive");
		var message = new TestMessage { Ratio = -0.1 };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void PositiveNumberConstraint_WithPositiveFloat_IsSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Factor", "Factor must be positive");
		var message = new TestMessage { Factor = 1.5f };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void PositiveNumberConstraint_WithNegativeFloat_IsNotSatisfied()
	{
		// Arrange
		var constraint = new PositiveNumberConstraint("Factor", "Must be positive");
		var message = new TestMessage { Factor = -0.5f };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void PositiveNumberConstraint_WithNonNumericType_IsSatisfied()
	{
		// Arrange - string field is not numeric, constraint passes
		var constraint = new PositiveNumberConstraint("Name", "Name must be positive");
		var message = new TestMessage { Name = "test" };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	// --- FutureDateConstraint ---

	[Fact]
	public void FutureDateConstraint_WithFutureDateTime_IsSatisfied()
	{
		// Arrange
		var constraint = new FutureDateConstraint("FutureDate", "Date must be future");
		var message = new TestMessage { FutureDate = DateTime.UtcNow.AddDays(1) };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void FutureDateConstraint_WithPastDateTime_IsNotSatisfied()
	{
		// Arrange
		var constraint = new FutureDateConstraint("FutureDate", "Date must be future");
		var message = new TestMessage { FutureDate = DateTime.UtcNow.AddDays(-1) };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void FutureDateConstraint_WithFutureDateTimeOffset_IsSatisfied()
	{
		// Arrange
		var constraint = new FutureDateConstraint("FutureDateOffset", "Date must be future");
		var message = new TestMessage { FutureDateOffset = DateTimeOffset.UtcNow.AddDays(1) };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void FutureDateConstraint_WithPastDateTimeOffset_IsNotSatisfied()
	{
		// Arrange
		var constraint = new FutureDateConstraint("FutureDateOffset", "Date must be future");
		var message = new TestMessage { FutureDateOffset = DateTimeOffset.UtcNow.AddDays(-1) };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeFalse();
	}

	[Fact]
	public void FutureDateConstraint_WithNonExistentField_IsSatisfied()
	{
		// Arrange - null field value returns true (optional)
		var constraint = new FutureDateConstraint("NonExistent", "Date must be future");
		var message = new TestMessage();

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	[Fact]
	public void FutureDateConstraint_WithNonDateType_IsSatisfied()
	{
		// Arrange - string field is not a date, passes the "_ => true" case
		var constraint = new FutureDateConstraint("Name", "Must be future date");
		var message = new TestMessage { Name = "not-a-date" };

		// Act & Assert
		constraint.IsSatisfied(message).ShouldBeTrue();
	}

	// --- BaseFieldConstraint (common behaviors) ---

	[Fact]
	public void BaseFieldConstraint_WithNullFieldName_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new NonEmptyStringConstraint(null!, "error"));
	}

	[Fact]
	public void BaseFieldConstraint_WithNullErrorMessage_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new NonEmptyStringConstraint("field", null!));
	}

	[Fact]
	public void BaseFieldConstraint_GetFieldValue_WithNullMessage_Throws()
	{
		// Arrange
		var constraint = new NonEmptyStringConstraint("Name", "error");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => constraint.IsSatisfied(null!));
	}
}
