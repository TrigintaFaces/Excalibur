// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Unit tests for <see cref="ConfigurationException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfigurationExceptionShould
{
	[Fact]
	public void InheritFromDispatchException()
	{
		// Arrange & Act
		var exception = new ConfigurationException();

		// Assert
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void HaveDefaultConstructor()
	{
		// Arrange & Act
		var exception = new ConfigurationException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ConfigurationInvalid);
		exception.Message.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AcceptMessage()
	{
		// Arrange
		const string message = "Custom configuration error";

		// Act
		var exception = new ConfigurationException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ErrorCode.ShouldBe(ErrorCodes.ConfigurationInvalid);
	}

	[Fact]
	public void AcceptMessageAndInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");
		const string message = "Configuration error";

		// Act
		var exception = new ConfigurationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.ErrorCode.ShouldBe(ErrorCodes.ConfigurationInvalid);
	}

	[Fact]
	public void AcceptErrorCodeAndMessage()
	{
		// Arrange
		const string errorCode = "CUSTOM_CONFIG";
		const string message = "Custom config error";

		// Act
		var exception = new ConfigurationException(errorCode, message);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void AcceptErrorCodeMessageAndInnerException()
	{
		// Arrange
		const string errorCode = "CUSTOM_CONFIG";
		const string message = "Custom config error";
		var innerException = new ArgumentNullException("param");

		// Act
		var exception = new ConfigurationException(errorCode, message, innerException);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void CreateMissingConfigurationException()
	{
		// Arrange
		const string configKey = "ConnectionStrings:Database";

		// Act
		var exception = ConfigurationException.Missing(configKey);

		// Assert
		exception.Message.ShouldContain(configKey);
		exception.Message.ShouldContain("missing");
		exception.Context.ShouldContainKey("configKey");
		exception.SuggestedAction.ShouldContain(configKey);
		exception.SuggestedAction.ShouldContain("appsettings.json");
		exception.DispatchStatusCode.ShouldBe(500);
	}

	[Fact]
	public void CreateInvalidConfigurationException()
	{
		// Arrange
		const string configKey = "MaxRetries";
		const int value = -5;
		const string reason = "Value must be non-negative";

		// Act
		var exception = ConfigurationException.Invalid(configKey, value, reason);

		// Assert
		exception.Message.ShouldContain(configKey);
		exception.Message.ShouldContain(reason);
		exception.Context.ShouldContainKey("configKey");
		exception.Context.ShouldContainKey("invalidValue");
		exception.Context.ShouldContainKey("reason");
		exception.SuggestedAction.ShouldContain(configKey);
		exception.SuggestedAction.ShouldContain(reason);
		exception.DispatchStatusCode.ShouldBe(500);
	}

	[Fact]
	public void CreateInvalidConfigurationExceptionWithNullValue()
	{
		// Arrange
		const string configKey = "ApiKey";
		const string reason = "Value cannot be null";

		// Act
		var exception = ConfigurationException.Invalid(configKey, null, reason);

		// Assert
		exception.Message.ShouldContain(configKey);
		exception.Message.ShouldContain(reason);
		exception.Context.ShouldContainKey("invalidValue");
	}

	[Fact]
	public void CreateSectionNotFoundExceptionException()
	{
		// Arrange
		const string sectionName = "Logging:LogLevel";

		// Act
		var exception = ConfigurationException.SectionNotFound(sectionName);

		// Assert
		exception.Message.ShouldContain(sectionName);
		exception.Message.ShouldContain("was not found");
		exception.Context.ShouldContainKey("sectionName");
		exception.SuggestedAction.ShouldContain(sectionName);
		exception.DispatchStatusCode.ShouldBe(500);
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.ConfigurationSectionNotFound);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ConfigurationException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeCatchableAsDispatchException()
	{
		// Arrange
		var exception = new ConfigurationException("Test error");

		// Act & Assert
		try
		{
			throw exception;
		}
		catch (DispatchException caught)
		{
			caught.ShouldBe(exception);
		}
	}

	[Theory]
	[InlineData("Database:ConnectionString")]
	[InlineData("AzureAd:ClientId")]
	[InlineData("Feature:Enabled")]
	[InlineData("Nested:Deep:Config:Value")]
	public void CreateMissingExceptionForVariousKeys(string configKey)
	{
		// Act
		var exception = ConfigurationException.Missing(configKey);

		// Assert
		exception.Message.ShouldContain(configKey);
		exception.Context.ShouldContainKeyAndValue("configKey", configKey);
	}

	[Theory]
	[InlineData("Timeout", 0, "Value must be positive")]
	[InlineData("Port", 99999, "Value must be between 1 and 65535")]
	[InlineData("Environment", "INVALID", "Value must be Development, Staging, or Production")]
	public void CreateInvalidExceptionForVariousScenarios(string configKey, object value, string reason)
	{
		// Act
		var exception = ConfigurationException.Invalid(configKey, value, reason);

		// Assert
		exception.Message.ShouldContain(configKey);
		exception.Message.ShouldContain(reason);
		exception.Context.ShouldContainKeyAndValue("configKey", configKey);
		exception.Context.ShouldContainKeyAndValue("reason", reason);
	}

	[Fact]
	public void ProvideHelpfulSuggestedActionsForMissingConfig()
	{
		// Arrange
		const string configKey = "Smtp:Server";

		// Act
		var exception = ConfigurationException.Missing(configKey);

		// Assert
		exception.SuggestedAction.ShouldContain("appsettings.json");
		exception.SuggestedAction.ShouldContain("environment variables");
	}
}
