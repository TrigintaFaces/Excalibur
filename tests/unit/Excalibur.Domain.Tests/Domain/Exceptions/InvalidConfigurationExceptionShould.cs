// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Exceptions;

namespace Excalibur.Tests.Domain.Exceptions;

/// <summary>
/// Unit tests for <see cref="InvalidConfigurationException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class InvalidConfigurationExceptionShould
{
	[Fact]
	public void Constructor_Default_CreatesExceptionWithDefaultMessage()
	{
		// Arrange & Act
		var exception = new InvalidConfigurationException();

		// Assert
		exception.Message.ShouldNotBeNullOrEmpty();
		exception.Setting.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Custom configuration error";

		// Act
		var exception = new InvalidConfigurationException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.Setting.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsProperties()
	{
		// Arrange
		const string message = "Configuration error with inner";
		var innerException = new ArgumentException("Inner error");

		// Act
		var exception = new InvalidConfigurationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.Setting.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithSettingAndStatusCode_SetsSettingProperty()
	{
		// Arrange
		const string setting = "ConnectionStrings:Database";

		// Act - using the 4-parameter constructor with setting
		var exception = new InvalidConfigurationException(setting, statusCode: 500, message: null, innerException: null);

		// Assert
		exception.Setting.ShouldBe(setting);
		exception.Message.ShouldContain(setting);
	}

	[Fact]
	public void Constructor_WithSettingAndStatusCode_SetsStatusCode()
	{
		// Arrange
		const string setting = "AppSettings:ApiKey";
		const int statusCode = 503;

		// Act
		var exception = new InvalidConfigurationException(setting, statusCode);

		// Assert
		exception.Setting.ShouldBe(setting);
		exception.StatusCode.ShouldBe(statusCode);
	}

	[Fact]
	public void Constructor_WithSettingAndDefaultStatusCode_UsesFiveHundred()
	{
		// Arrange
		const string setting = "MissingSetting";

		// Act
		var exception = new InvalidConfigurationException(setting, statusCode: null);

		// Assert
		exception.StatusCode.ShouldBe(500);
	}

	[Fact]
	public void Constructor_WithAllSettingParameters_SetsAllProperties()
	{
		// Arrange
		const string setting = "Config:Key";
		const int statusCode = 400;
		const string message = "Custom error message";
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new InvalidConfigurationException(setting, statusCode, message, innerException);

		// Assert
		exception.Setting.ShouldBe(setting);
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void Constructor_WithSettingAndNullMessage_UsesDefaultMessage()
	{
		// Arrange
		const string setting = "MissingSetting";

		// Act
		var exception = new InvalidConfigurationException(setting, statusCode: 500, message: null);

		// Assert
		exception.Message.ShouldContain(setting);
	}

	[Fact]
	public void Constructor_WithStatusCodeMessageAndInnerException_SetsProperties()
	{
		// Arrange
		const int statusCode = 503;
		const string message = "Service unavailable";
		var innerException = new TimeoutException("Timeout");

		// Act
		var exception = new InvalidConfigurationException(statusCode, message, innerException);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.Setting.ShouldBe(string.Empty);
	}

	[Fact]
	public void InheritsFromApiException()
	{
		// Arrange & Act
		var exception = new InvalidConfigurationException("test setting", statusCode: 500);

		// Assert
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Constructor_WithNullSettingAndFullParams_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InvalidConfigurationException(null!, statusCode: 500, message: "test"));
	}

	[Fact]
	public void Setting_Property_CanBeAccessed()
	{
		// Arrange
		const string expectedSetting = "Database:ConnectionString";
		var exception = new InvalidConfigurationException(expectedSetting, statusCode: 500);

		// Act & Assert
		exception.Setting.ShouldBe(expectedSetting);
	}

	[Fact]
	public void Constructor_WithDifferentStatusCodes_SetsCorrectly()
	{
		// Arrange & Act
		var notFound = new InvalidConfigurationException("setting1", 404);
		var serverError = new InvalidConfigurationException("setting2", 500);
		var badRequest = new InvalidConfigurationException("setting3", 400);

		// Assert
		notFound.StatusCode.ShouldBe(404);
		serverError.StatusCode.ShouldBe(500);
		badRequest.StatusCode.ShouldBe(400);
	}
}
