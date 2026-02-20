// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Unit tests for <see cref="ErrorCategory"/>.
/// </summary>
/// <remarks>
/// Tests the error category enumeration values.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Exceptions")]
[Trait("Priority", "0")]
public sealed class ErrorCategoryShould
{
	#region Enum Value Tests

	[Fact]
	public void Unknown_HasValue0()
	{
		// Assert
		((int)ErrorCategory.Unknown).ShouldBe(0);
	}

	[Fact]
	public void Configuration_HasValue1()
	{
		// Assert
		((int)ErrorCategory.Configuration).ShouldBe(1);
	}

	[Fact]
	public void Validation_HasValue2()
	{
		// Assert
		((int)ErrorCategory.Validation).ShouldBe(2);
	}

	[Fact]
	public void Messaging_HasValue3()
	{
		// Assert
		((int)ErrorCategory.Messaging).ShouldBe(3);
	}

	[Fact]
	public void Serialization_HasValue4()
	{
		// Assert
		((int)ErrorCategory.Serialization).ShouldBe(4);
	}

	[Fact]
	public void Network_HasValue5()
	{
		// Assert
		((int)ErrorCategory.Network).ShouldBe(5);
	}

	[Fact]
	public void Security_HasValue6()
	{
		// Assert
		((int)ErrorCategory.Security).ShouldBe(6);
	}

	[Fact]
	public void Data_HasValue7()
	{
		// Assert
		((int)ErrorCategory.Data).ShouldBe(7);
	}

	[Fact]
	public void Timeout_HasValue8()
	{
		// Assert
		((int)ErrorCategory.Timeout).ShouldBe(8);
	}

	[Fact]
	public void Resource_HasValue9()
	{
		// Assert
		((int)ErrorCategory.Resource).ShouldBe(9);
	}

	[Fact]
	public void System_HasValue10()
	{
		// Assert
		((int)ErrorCategory.System).ShouldBe(10);
	}

	[Fact]
	public void Resilience_HasValue11()
	{
		// Assert
		((int)ErrorCategory.Resilience).ShouldBe(11);
	}

	[Fact]
	public void Concurrency_HasValue12()
	{
		// Assert
		((int)ErrorCategory.Concurrency).ShouldBe(12);
	}

	#endregion

	#region Enum Completeness Tests

	[Fact]
	public void HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<ErrorCategory>();

		// Assert
		values.Length.ShouldBe(13);
	}

	[Theory]
	[InlineData(ErrorCategory.Unknown, "Unknown")]
	[InlineData(ErrorCategory.Configuration, "Configuration")]
	[InlineData(ErrorCategory.Validation, "Validation")]
	[InlineData(ErrorCategory.Messaging, "Messaging")]
	[InlineData(ErrorCategory.Serialization, "Serialization")]
	[InlineData(ErrorCategory.Network, "Network")]
	[InlineData(ErrorCategory.Security, "Security")]
	[InlineData(ErrorCategory.Data, "Data")]
	[InlineData(ErrorCategory.Timeout, "Timeout")]
	[InlineData(ErrorCategory.Resource, "Resource")]
	[InlineData(ErrorCategory.System, "System")]
	[InlineData(ErrorCategory.Resilience, "Resilience")]
	[InlineData(ErrorCategory.Concurrency, "Concurrency")]
	public void ToString_ReturnsExpectedName(ErrorCategory category, string expectedName)
	{
		// Act
		var result = category.ToString();

		// Assert
		result.ShouldBe(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Unknown", ErrorCategory.Unknown)]
	[InlineData("Configuration", ErrorCategory.Configuration)]
	[InlineData("Validation", ErrorCategory.Validation)]
	[InlineData("Messaging", ErrorCategory.Messaging)]
	[InlineData("Serialization", ErrorCategory.Serialization)]
	[InlineData("Network", ErrorCategory.Network)]
	[InlineData("Security", ErrorCategory.Security)]
	[InlineData("Data", ErrorCategory.Data)]
	[InlineData("Timeout", ErrorCategory.Timeout)]
	[InlineData("Resource", ErrorCategory.Resource)]
	[InlineData("System", ErrorCategory.System)]
	[InlineData("Resilience", ErrorCategory.Resilience)]
	[InlineData("Concurrency", ErrorCategory.Concurrency)]
	public void Parse_WithValidString_ReturnsExpectedCategory(string input, ErrorCategory expected)
	{
		// Act
		var result = Enum.Parse<ErrorCategory>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithInvalidString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<ErrorCategory>("InvalidCategory"));
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var result = Enum.TryParse<ErrorCategory>("InvalidCategory", out _);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsDefined Tests

	[Theory]
	[InlineData(0, true)]
	[InlineData(12, true)]
	[InlineData(13, false)]
	[InlineData(-1, false)]
	[InlineData(100, false)]
	public void IsDefined_WithIntValue_ReturnsExpected(int value, bool expected)
	{
		// Act
		var result = Enum.IsDefined(typeof(ErrorCategory), value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void SwitchExpression_CanMatchAllCategories()
	{
		// Arrange
		var categories = Enum.GetValues<ErrorCategory>();

		// Act & Assert - Ensure switch expression covers all cases
		foreach (var category in categories)
		{
			var description = category switch
			{
				ErrorCategory.Unknown => "Unclassified",
				ErrorCategory.Configuration => "Config issue",
				ErrorCategory.Validation => "Input validation",
				ErrorCategory.Messaging => "Queue/broker issue",
				ErrorCategory.Serialization => "Serialization issue",
				ErrorCategory.Network => "Connectivity issue",
				ErrorCategory.Security => "Auth issue",
				ErrorCategory.Data => "Database issue",
				ErrorCategory.Timeout => "Operation timed out",
				ErrorCategory.Resource => "Resource unavailable",
				ErrorCategory.System => "System error",
				ErrorCategory.Resilience => "Circuit breaker",
				ErrorCategory.Concurrency => "Thread safety",
				_ => "Other",
			};

			description.ShouldNotBeNullOrEmpty();
		}
	}

	[Fact]
	public void CanBeUsedAsRetriableIndicator()
	{
		// Arrange - Categories that typically indicate retriable errors
		var retriableCategories = new[]
		{
			ErrorCategory.Network,
			ErrorCategory.Timeout,
			ErrorCategory.Messaging,
			ErrorCategory.Resilience,
		};

		// Arrange - Categories that typically indicate non-retriable errors
		var nonRetriableCategories = new[]
		{
			ErrorCategory.Validation,
			ErrorCategory.Security,
			ErrorCategory.Configuration,
		};

		// Assert
		foreach (var category in retriableCategories)
		{
			IsRetriable(category).ShouldBeTrue($"{category} should be retriable");
		}

		foreach (var category in nonRetriableCategories)
		{
			IsRetriable(category).ShouldBeFalse($"{category} should not be retriable");
		}
	}

	private static bool IsRetriable(ErrorCategory category)
	{
		return category is ErrorCategory.Network
			or ErrorCategory.Timeout
			or ErrorCategory.Messaging
			or ErrorCategory.Resilience
			or ErrorCategory.Resource;
	}

	#endregion
}
