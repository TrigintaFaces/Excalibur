// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;
using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="MessageBrokerValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class MessageBrokerValidatorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void SetPriorityTo30()
	{
		// Act
		var validator = new TestableBrokerValidator("TestBroker");

		// Assert
		validator.Priority.ShouldBe(30);
	}

	[Fact]
	public void SetConfigurationNameFromConstructor()
	{
		// Act
		var validator = new TestableBrokerValidator("MyBroker");

		// Assert
		validator.ConfigurationName.ShouldBe("MyBroker");
	}

	#endregion

	#region ValidateEndpoint Tests

	[Fact]
	public void ValidateEndpoint_ReturnsTrue_WhenEndpointIsValid()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableBrokerValidator.TestValidateEndpoint("amqp://localhost:5672", errors, "Endpoint");

		// Assert
		result.ShouldBeTrue();
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateEndpoint_ReturnsTrue_WhenEndpointMatchesExpectedScheme()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableBrokerValidator.TestValidateEndpoint("amqp://localhost:5672", errors, "Endpoint", "amqp");

		// Assert
		result.ShouldBeTrue();
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateEndpoint_ReturnsFalse_WhenEndpointIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableBrokerValidator.TestValidateEndpoint(null, errors, "Endpoint");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing or empty");
	}

	[Fact]
	public void ValidateEndpoint_ReturnsFalse_WhenEndpointIsEmpty()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableBrokerValidator.TestValidateEndpoint("", errors, "Endpoint");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing or empty");
	}

	[Fact]
	public void ValidateEndpoint_ReturnsFalse_WhenEndpointIsWhitespace()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableBrokerValidator.TestValidateEndpoint("   ", errors, "Endpoint");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateEndpoint_ReturnsFalse_WhenEndpointIsNotValidUrl()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableBrokerValidator.TestValidateEndpoint("not-a-valid-url", errors, "Endpoint");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("Invalid endpoint URL format");
	}

	[Fact]
	public void ValidateEndpoint_ReturnsFalse_WhenSchemeDoesNotMatch()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableBrokerValidator.TestValidateEndpoint("http://localhost:5672", errors, "Endpoint", "amqp");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("Invalid URL scheme 'http'");
		errors[0].Message.ShouldContain("expected 'amqp'");
	}

	[Fact]
	public void ValidateEndpoint_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableBrokerValidator.TestValidateEndpoint("amqp://localhost:5672", null!, "Endpoint"));
	}

	[Fact]
	public void ValidateEndpoint_SetsConfigPath_InError()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		const string configPath = "MyBroker:ConnectionString";

		// Act
		_ = TestableBrokerValidator.TestValidateEndpoint(null, errors, configPath);

		// Assert
		errors[0].ConfigurationPath.ShouldBe(configPath);
	}

	[Fact]
	public void ValidateEndpoint_IncludesRecommendation_InError()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		_ = TestableBrokerValidator.TestValidateEndpoint("invalid", errors, "Endpoint", "kafka");

		// Assert
		errors[0].Recommendation.ShouldNotBeNull();
		errors[0].Recommendation.ShouldContain("valid URL");
	}

	#endregion

	#region Test Helper

	/// <summary>
	/// Testable concrete implementation of MessageBrokerValidator.
	/// </summary>
	private sealed class TestableBrokerValidator : MessageBrokerValidator
	{
		public TestableBrokerValidator(string configurationName)
			: base(configurationName)
		{
		}

		public override Task<ConfigurationValidationResult> ValidateAsync(
			IConfiguration configuration,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(ConfigurationValidationResult.Success());

		public static bool TestValidateEndpoint(
			string? endpoint,
			ICollection<ConfigurationValidationError> errors,
			string configPath,
			string? expectedScheme = null)
			=> ValidateEndpoint(endpoint, errors, configPath, expectedScheme);
	}

	#endregion
}
