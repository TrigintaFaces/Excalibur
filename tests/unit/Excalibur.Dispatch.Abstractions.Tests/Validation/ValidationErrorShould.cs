// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="ValidationError"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Validation")]
[Trait("Priority", "0")]
public sealed class ValidationErrorShould
{
	#region Constructor (Message Only) Tests

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Act
		var error = new ValidationError("Test error message");

		// Assert
		error.Message.ShouldBe("Test error message");
	}

	[Fact]
	public void Constructor_WithMessage_PropertyNameIsNull()
	{
		// Act
		var error = new ValidationError("Test error message");

		// Assert
		error.PropertyName.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithNullMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ValidationError(null!));
	}

	#endregion

	#region Constructor (PropertyName and Message) Tests

	[Fact]
	public void Constructor_WithPropertyNameAndMessage_SetsPropertyName()
	{
		// Act
		var error = new ValidationError("TestProperty", "Test error message");

		// Assert
		error.PropertyName.ShouldBe("TestProperty");
	}

	[Fact]
	public void Constructor_WithPropertyNameAndMessage_SetsMessage()
	{
		// Act
		var error = new ValidationError("TestProperty", "Test error message");

		// Assert
		error.Message.ShouldBe("Test error message");
	}

	[Fact]
	public void Constructor_WithPropertyNameAndNullMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ValidationError("TestProperty", null!));
	}

	[Fact]
	public void Constructor_WithNullPropertyNameAndMessage_SetsNullPropertyName()
	{
		// Act
		var error = new ValidationError(null!, "Test error message");

		// Assert
		error.PropertyName.ShouldBeNull();
		error.Message.ShouldBe("Test error message");
	}

	#endregion

	#region ErrorCode Property Tests

	[Fact]
	public void ErrorCode_CanBeSetAndRetrieved()
	{
		// Arrange
		var error = new ValidationError("Test error");

		// Act
		error.ErrorCode = "ERR001";

		// Assert
		error.ErrorCode.ShouldBe("ERR001");
	}

	[Fact]
	public void ErrorCode_DefaultIsNull()
	{
		// Act
		var error = new ValidationError("Test error");

		// Assert
		error.ErrorCode.ShouldBeNull();
	}

	[Fact]
	public void ErrorCode_CanBeSetToNull()
	{
		// Arrange
		var error = new ValidationError("Test error") { ErrorCode = "ERR001" };

		// Act
		error.ErrorCode = null;

		// Assert
		error.ErrorCode.ShouldBeNull();
	}

	#endregion

	#region Metadata Property Tests

	[Fact]
	public void Metadata_DefaultIsNull()
	{
		// Act
		var error = new ValidationError("Test error");

		// Assert
		error.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Metadata_CanBeInitialized()
	{
		// Act
		var error = new ValidationError("Test error")
		{
			Metadata = new Dictionary<string, object> { ["key"] = "value" },
		};

		// Assert
		_ = error.Metadata.ShouldNotBeNull();
		error.Metadata.ShouldContainKeyAndValue("key", "value");
	}

	[Fact]
	public void Metadata_CanContainMultipleEntries()
	{
		// Act
		var error = new ValidationError("Test error")
		{
			Metadata = new Dictionary<string, object>
			{
				["key1"] = "value1",
				["key2"] = 42,
				["key3"] = true,
			},
		};

		// Assert
		error.Metadata.Count.ShouldBe(3);
	}

	[Fact]
	public void Metadata_CanContainDifferentValueTypes()
	{
		// Act
		var error = new ValidationError("Test error")
		{
			Metadata = new Dictionary<string, object>
			{
				["string"] = "text",
				["int"] = 123,
				["bool"] = false,
				["double"] = 3.14,
			},
		};

		// Assert
		error.Metadata["string"].ShouldBe("text");
		error.Metadata["int"].ShouldBe(123);
		error.Metadata["bool"].ShouldBe(false);
		error.Metadata["double"].ShouldBe(3.14);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var error = new ValidationError("PropertyName", "Error message")
		{
			ErrorCode = "ERR100",
			Metadata = new Dictionary<string, object> { ["detail"] = "extra info" },
		};

		// Assert
		error.PropertyName.ShouldBe("PropertyName");
		error.Message.ShouldBe("Error message");
		error.ErrorCode.ShouldBe("ERR100");
		error.Metadata.ShouldContainKeyAndValue("detail", "extra info");
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void Constructor_WithEmptyMessage_AcceptsEmptyString()
	{
		// Act
		var error = new ValidationError(string.Empty);

		// Assert
		error.Message.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithWhitespaceMessage_AcceptsWhitespace()
	{
		// Act
		var error = new ValidationError("   ");

		// Assert
		error.Message.ShouldBe("   ");
	}

	[Fact]
	public void Constructor_WithEmptyPropertyName_AcceptsEmptyString()
	{
		// Act
		var error = new ValidationError(string.Empty, "Error message");

		// Assert
		error.PropertyName.ShouldBe(string.Empty);
	}

	#endregion
}
