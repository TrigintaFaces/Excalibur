using Excalibur.Core.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Exceptions;

public class InvalidConfigurationExceptionShould
{
	[Fact]
	public void InitializeWithDefaultMessageAndStatusCode()
	{
		// Arrange
		var setting = "DatabaseConnection";

		// Act
		var exception = new InvalidConfigurationException(setting);

		// Assert
		exception.Message.ShouldBe("The 'DatabaseConnection' setting is missing or invalid.");
		exception.StatusCode.ShouldBe(500);
		exception.Setting.ShouldBe(setting);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void InitializeWithCustomMessage()
	{
		// Arrange
		var setting = "APIKey";
		var message = "Custom error message for APIKey";

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new InvalidConfigurationException(setting, null, message);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.Message.ShouldBe(message);
		exception.StatusCode.ShouldBe(500);
		exception.Setting.ShouldBe(setting);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void InitializeWithCustomStatusCodeAndMessage()
	{
		// Arrange
		var setting = "ServiceUrl";
		var statusCode = 400;
		var message = "Invalid URL configuration";

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new InvalidConfigurationException(setting, statusCode, message);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.Setting.ShouldBe(setting);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void InitializeWithInnerException()
	{
		// Arrange
		var setting = "Timeout";
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var innerException = new InvalidOperationException("Inner exception message");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		var exception = new InvalidConfigurationException(setting, null, null, innerException);

		// Assert
		exception.Message.ShouldBe("The 'Timeout' setting is missing or invalid.");
		exception.InnerException.ShouldBe(innerException);
		exception.StatusCode.ShouldBe(500);
		exception.Setting.ShouldBe(setting);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void ThrowArgumentNullExceptionIfSettingIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InvalidConfigurationException(null!)).ParamName.ShouldBe("setting");
	}

	[Fact]
	public void UseDefaultMessageIfNullMessageProvided()
	{
		// Arrange
		var setting = "LoggingLevel";

		// Act
		var exception = new InvalidConfigurationException(setting, 400, null);

		// Assert
		exception.Message.ShouldBe("The 'LoggingLevel' setting is missing or invalid.");
		exception.StatusCode.ShouldBe(400);
	}

	[Fact]
	public void GenerateUniqueIdsForDifferentInstances()
	{
		// Arrange & Act
		var exception1 = new InvalidConfigurationException("Setting1");
		var exception2 = new InvalidConfigurationException("Setting2");

		// Assert
		exception1.Id.ShouldNotBe(exception2.Id);
	}

	[Fact]
	public void ReturnCorrectStatusCodeFromStaticMethod()
	{
		// Arrange
		var statusCode = 403;
		var exception = new InvalidConfigurationException("AuthToken", statusCode);

		// Act
		var result = ApiException.GetStatusCode(exception);

		// Assert
		result.ShouldBe(statusCode);
	}

	[Fact]
	public void Return500ForNonApiExceptionInGetStatusCode()
	{
		// Arrange
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new InvalidOperationException("Not an API exception");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		var result = ApiException.GetStatusCode(exception);

		// Assert
		result.ShouldBe(500);
	}

	[Fact]
	public void PreserveSettingPropertyCorrectly()
	{
		// Arrange
		var setting = "CacheDuration";

		// Act
		var exception = new InvalidConfigurationException(setting);

		// Assert
		exception.Setting.ShouldBe(setting);
	}
}
