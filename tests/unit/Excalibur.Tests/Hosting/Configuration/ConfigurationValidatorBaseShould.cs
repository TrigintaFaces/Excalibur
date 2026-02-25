// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

using ConfigValidationError = Excalibur.Hosting.Configuration.ConfigurationValidationError;
using ConfigValidationResult = Excalibur.Hosting.Configuration.ConfigurationValidationResult;

namespace Excalibur.Tests.Hosting.Configuration;

[Trait("Category", "Unit")]
public sealed class ConfigurationValidatorBaseShould
{
	[Fact]
	public void InitializeWithCorrectProperties()
	{
		// Arrange & Act
		var validator = new TestValidator("TestConfig", 50);

		// Assert
		validator.ConfigurationName.ShouldBe("TestConfig");
		validator.Priority.ShouldBe(50);
	}

	[Fact]
	public void ThrowWhenConfigurationNameIsEmpty()
	{
		// Arrange, Act & Assert
		_ = Should.Throw<ArgumentException>(static () => new TestValidator("", 50));
		_ = Should.Throw<ArgumentException>(static () => new TestValidator(" ", 50));
	}

	[Fact]
	public async Task ValidateRequiredValueSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RequiredKey"] = "value",
				["IntKey"] = "50",
				["TimeSpanKey"] = "00:01:30",
				["EnumKey"] = "prod",
			})
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task FailWhenRequiredValueIsMissing()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
		result.Errors.Any(static e => e.ConfigurationPath == "RequiredKey").ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateIntRangeSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RequiredKey"] = "value",
				["IntKey"] = "50",
				["TimeSpanKey"] = "00:01:30",
				["EnumKey"] = "prod",
			})
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task FailWhenIntIsOutOfRange()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RequiredKey"] = "value",
				["IntKey"] = "150", // Out of range (1-100)
				["TimeSpanKey"] = "00:01:30",
				["EnumKey"] = "prod",
			})
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Any(static e => e.ConfigurationPath == "IntKey").ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateTimeSpanSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RequiredKey"] = "value",
				["IntKey"] = "50",
				["TimeSpanKey"] = "00:01:30", // 1 minute 30 seconds
				["EnumKey"] = "prod",
			})
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task FailWhenTimeSpanIsInvalid()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RequiredKey"] = "value",
				["IntKey"] = "50",
				["TimeSpanKey"] = "invalid",
				["EnumKey"] = "prod",
			})
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Any(static e => e.ConfigurationPath == "TimeSpanKey").ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateEnumSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RequiredKey"] = "value",
				["IntKey"] = "50",
				["TimeSpanKey"] = "00:01:30",
				["EnumKey"] = "prod",
			})
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task FailWhenEnumValueIsInvalid()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RequiredKey"] = "value",
				["IntKey"] = "50",
				["TimeSpanKey"] = "00:01:30",
				["EnumKey"] = "invalid",
			})
			.Build();

		var validator = new TestValidator("Test", 100);

		// Act
		var result = await validator.ValidateAsync(configuration).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Any(static e => e.ConfigurationPath == "EnumKey").ShouldBeTrue();
	}

	private sealed class TestValidator(string name, int priority) : ConfigurationValidatorBase(name, priority)
	{
		public override Task<ConfigValidationResult> ValidateAsync(
			IConfiguration configuration,
			CancellationToken cancellationToken = default)
		{
			var errors = new List<ConfigValidationError>();

			// Test the protected validation methods
			_ = ValidateRequired(configuration, "RequiredKey", errors);
			_ = ValidateIntRange(configuration, "IntKey", 1, 100, errors);
			_ = ValidateTimeSpan(configuration, "TimeSpanKey", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), errors);

			var allowedValues = new HashSet<string> { "dev", "staging", "prod" };
			_ = ValidateEnum(configuration, "EnumKey", allowedValues, errors);

			return Task.FromResult(errors.Count == 0
				? ConfigValidationResult.Success()
				: ConfigValidationResult.Failure(errors));
		}
	}
}
