// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidatorBase"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidatorBaseShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentException_WhenConfigurationNameIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TestableValidator(null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenConfigurationNameIsEmpty()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TestableValidator(""));
	}

	[Fact]
	public void ThrowArgumentException_WhenConfigurationNameIsWhitespace()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TestableValidator("   "));
	}

	[Fact]
	public void SetConfigurationNameProperty()
	{
		// Arrange
		const string name = "TestConfig";

		// Act
		var validator = new TestableValidator(name);

		// Assert
		validator.ConfigurationName.ShouldBe(name);
	}

	[Fact]
	public void SetDefaultPriorityTo100()
	{
		// Act
		var validator = new TestableValidator("TestConfig");

		// Assert
		validator.Priority.ShouldBe(100);
	}

	[Fact]
	public void SetCustomPriority_WhenProvided()
	{
		// Act
		var validator = new TestableValidator("TestConfig", priority: 50);

		// Assert
		validator.Priority.ShouldBe(50);
	}

	#endregion

	#region ValidateRequired Tests

	[Fact]
	public void ValidateRequired_ReturnsValue_WhenValueExists()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "TestValue"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateRequired(config, "TestKey", errors);

		// Assert
		result.ShouldBe("TestValue");
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateRequired_AddsError_WhenValueIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateRequired(config, "MissingKey", errors);

		// Assert
		result.ShouldBeNull();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing or empty");
		errors[0].ConfigurationPath.ShouldBe("MissingKey");
	}

	[Fact]
	public void ValidateRequired_AddsError_WhenValueIsEmpty()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = ""
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateRequired(config, "TestKey", errors);

		// Assert
		result.ShouldBeNull();
		errors.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateRequired_AddsError_WhenValueIsWhitespace()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "   "
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateRequired(config, "TestKey", errors);

		// Assert
		result.ShouldBeNull();
		errors.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateRequired_ReturnsNull_WhenNotRequired_AndValueMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateRequired(config, "MissingKey", errors, isRequired: false);

		// Assert
		result.ShouldBeNull();
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateRequired_ThrowsArgumentNull_WhenConfigurationIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateRequired(null!, "TestKey", errors));
	}

	[Fact]
	public void ValidateRequired_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateRequired(config, "TestKey", null!));
	}

	#endregion

	#region ValidateIntRange Tests

	[Fact]
	public void ValidateIntRange_ReturnsValue_WhenWithinRange()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "50"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "TestKey", 1, 100, errors);

		// Assert
		result.ShouldBe(50);
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateIntRange_ReturnsMinValue_WhenWithinRangeAtMin()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "1"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "TestKey", 1, 100, errors);

		// Assert
		result.ShouldBe(1);
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateIntRange_ReturnsMaxValue_WhenWithinRangeAtMax()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "100"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "TestKey", 1, 100, errors);

		// Assert
		result.ShouldBe(100);
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateIntRange_ReturnsDefault_WhenValueMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "MissingKey", 1, 100, errors, defaultValue: 25);

		// Assert
		result.ShouldBe(25);
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateIntRange_AddsError_WhenValueMissing_NoDefault()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "MissingKey", 1, 100, errors);

		// Assert
		result.ShouldBe(1); // Returns min
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing");
	}

	[Fact]
	public void ValidateIntRange_AddsError_WhenValueNotInteger()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "abc"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "TestKey", 1, 100, errors, defaultValue: 50);

		// Assert
		result.ShouldBe(50);
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("not a valid integer");
	}

	[Fact]
	public void ValidateIntRange_AddsError_WhenValueBelowMin()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "0"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "TestKey", 1, 100, errors, defaultValue: 25);

		// Assert
		result.ShouldBe(25);
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("outside the valid range");
	}

	[Fact]
	public void ValidateIntRange_AddsError_WhenValueAboveMax()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "101"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateIntRange(config, "TestKey", 1, 100, errors, defaultValue: 25);

		// Assert
		result.ShouldBe(25);
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("outside the valid range");
	}

	[Fact]
	public void ValidateIntRange_ThrowsArgumentNull_WhenConfigurationIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateIntRange(null!, "TestKey", 1, 100, errors));
	}

	[Fact]
	public void ValidateIntRange_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateIntRange(config, "TestKey", 1, 100, null!));
	}

	#endregion

	#region ValidateTimeSpan Tests

	[Fact]
	public void ValidateTimeSpan_ReturnsValue_WhenValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "00:30:00"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateTimeSpan(config, "TestKey", null, null, errors);

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(30));
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateTimeSpan_ReturnsDefault_WhenMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var defaultValue = TimeSpan.FromHours(1);

		// Act
		var result = TestableValidator.CallValidateTimeSpan(config, "MissingKey", null, null, errors, defaultValue);

		// Assert
		result.ShouldBe(defaultValue);
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateTimeSpan_AddsError_WhenMissing_NoDefault()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateTimeSpan(config, "MissingKey", null, null, errors);

		// Assert
		result.ShouldBe(TimeSpan.Zero);
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing");
	}

	[Fact]
	public void ValidateTimeSpan_AddsError_WhenInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "not-a-timespan"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableValidator.CallValidateTimeSpan(config, "TestKey", null, null, errors, TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(5));
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("not a valid TimeSpan");
	}

	[Fact]
	public void ValidateTimeSpan_AddsError_WhenBelowMin()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "00:00:30"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var minValue = TimeSpan.FromMinutes(1);

		// Act
		var result = TestableValidator.CallValidateTimeSpan(config, "TestKey", minValue, null, errors, TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(5));
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("less than the minimum");
	}

	[Fact]
	public void ValidateTimeSpan_AddsError_WhenAboveMax()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "02:00:00"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var maxValue = TimeSpan.FromHours(1);

		// Act
		var result = TestableValidator.CallValidateTimeSpan(config, "TestKey", null, maxValue, errors, TimeSpan.FromMinutes(30));

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(30));
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("exceeds the maximum");
	}

	[Fact]
	public void ValidateTimeSpan_ThrowsArgumentNull_WhenConfigurationIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateTimeSpan(null!, "TestKey", null, null, errors));
	}

	[Fact]
	public void ValidateTimeSpan_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateTimeSpan(config, "TestKey", null, null, null!));
	}

	#endregion

	#region ValidateEnum Tests

	[Fact]
	public void ValidateEnum_ReturnsValue_WhenValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "Option1"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var allowedValues = new HashSet<string>(["Option1", "Option2", "Option3"]);

		// Act
		var result = TestableValidator.CallValidateEnum(config, "TestKey", allowedValues, errors);

		// Assert
		result.ShouldBe("Option1");
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateEnum_ReturnsValue_CaseInsensitive()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "option1"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var allowedValues = new HashSet<string>(["Option1", "Option2", "Option3"]);

		// Act
		var result = TestableValidator.CallValidateEnum(config, "TestKey", allowedValues, errors);

		// Assert
		result.ShouldBe("option1");
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateEnum_ReturnsDefault_WhenMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var allowedValues = new HashSet<string>(["Option1", "Option2"]);

		// Act
		var result = TestableValidator.CallValidateEnum(config, "MissingKey", allowedValues, errors, defaultValue: "Option1");

		// Assert
		result.ShouldBe("Option1");
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateEnum_AddsError_WhenMissing_NoDefault()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var allowedValues = new HashSet<string>(["Option1", "Option2"]);

		// Act
		var result = TestableValidator.CallValidateEnum(config, "MissingKey", allowedValues, errors);

		// Assert
		result.ShouldBeNull();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing");
	}

	[Fact]
	public void ValidateEnum_AddsError_WhenInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["TestKey"] = "InvalidOption"
			})
			.Build();
		var errors = new List<ConfigurationValidationError>();
		var allowedValues = new HashSet<string>(["Option1", "Option2"]);

		// Act
		var result = TestableValidator.CallValidateEnum(config, "TestKey", allowedValues, errors, defaultValue: "Option1");

		// Assert
		result.ShouldBe("Option1");
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("not valid");
	}

	[Fact]
	public void ValidateEnum_ThrowsArgumentNull_WhenConfigurationIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		var allowedValues = new HashSet<string>(["Option1"]);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateEnum(null!, "TestKey", allowedValues, errors));
	}

	[Fact]
	public void ValidateEnum_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();
		var allowedValues = new HashSet<string>(["Option1"]);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableValidator.CallValidateEnum(config, "TestKey", allowedValues, null!));
	}

	#endregion

	#region Test Helper

	/// <summary>
	/// Testable concrete implementation of ConfigurationValidatorBase for testing protected methods.
	/// </summary>
	private sealed class TestableValidator : ConfigurationValidatorBase
	{
		public TestableValidator(string configurationName, int priority = 100)
			: base(configurationName, priority)
		{
		}

		public override Task<ConfigurationValidationResult> ValidateAsync(
			IConfiguration configuration,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(ConfigurationValidationResult.Success());

		public static string? CallValidateRequired(
			IConfiguration configuration,
			string key,
			ICollection<ConfigurationValidationError> errors,
			bool isRequired = true)
			=> ValidateRequired(configuration, key, errors, isRequired);

		public static int CallValidateIntRange(
			IConfiguration configuration,
			string key,
			int min,
			int max,
			ICollection<ConfigurationValidationError> errors,
			int? defaultValue = null)
			=> ValidateIntRange(configuration, key, min, max, errors, defaultValue);

		public static TimeSpan CallValidateTimeSpan(
			IConfiguration configuration,
			string key,
			TimeSpan? minValue,
			TimeSpan? maxValue,
			ICollection<ConfigurationValidationError> errors,
			TimeSpan? defaultValue = null)
			=> ValidateTimeSpan(configuration, key, minValue, maxValue, errors, defaultValue);

		public static string? CallValidateEnum(
			IConfiguration configuration,
			string key,
			IReadOnlySet<string> allowedValues,
			ICollection<ConfigurationValidationError> errors,
			StringComparison comparison = StringComparison.OrdinalIgnoreCase,
			string? defaultValue = null)
			=> ValidateEnum(configuration, key, allowedValues, errors, comparison, defaultValue);
	}

	#endregion
}
