using Excalibur.Core;
using Excalibur.Core.Exceptions;
using Excalibur.Tests.Mothers.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class ApplicationContextShould : IAsyncDisposable
{
	public ApplicationContextShould()
	{
		// Reset at the start of each test
		ApplicationContextMother.Reset();
	}

	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void InitializeWithProvidedDictionary()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "ApplicationName", "TestApp" }, { "ApplicationSystemName", "TestAppSystem" } };

		// Act
		ApplicationContext.Init(context);

		// Assert
		ApplicationContext.ApplicationName.ShouldBe("TestApp");
		ApplicationContext.ApplicationSystemName.ShouldBe("TestAppSystem");
	}

	[Fact]
	public void ThrowArgumentNullExceptionIfInitWithNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => ApplicationContext.Init(null!));
	}

	[Fact]
	public void ReturnCorrectValueForGet()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "ServiceAccountName", "TestService" } };

		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Get("ServiceAccountName");

		// Assert
		result.ShouldBe("TestService");
	}

	[Fact]
	public void ThrowInvalidConfigurationExceptionIfKeyIsMissing()
	{
		// Act & Assert
		_ = Should.Throw<InvalidConfigurationException>(() => ApplicationContext.Get("MissingKey"));
	}

	[Fact]
	public void ReturnDefaultValueIfKeyIsMissing()
	{
		// Act
		var result = ApplicationContext.Get("MissingKey", "DefaultValue");

		// Assert
		result.ShouldBe("DefaultValue");
	}

	[Fact]
	public void ExpandPlaceholdersWithContextValues()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "BaseUrl", "https://api.example.com" }, { "Endpoint", "%BaseUrl%/endpoint" } };

		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Expand("%Endpoint%");

		// Assert
		result.ShouldBe("https://api.example.com/endpoint");
	}

	[Fact]
	public void ExpandPlaceholdersWithEnvironmentVariables()
	{
		// Arrange
		Environment.SetEnvironmentVariable("API_KEY", "test-key");

		// Act
		var result = ApplicationContext.Expand("%API_KEY%");

		// Assert
		result.ShouldBe("test-key");

		// Cleanup
		Environment.SetEnvironmentVariable("API_KEY", null);
	}

	[Fact]
	public void DetectAndThrowForCircularReferences()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "Key1", "%Key2%" }, { "Key2", "%Key1%" } };

		ApplicationContext.Init(context);

		// Act & Assert
		var ex = Should.Throw<InvalidConfigurationException>(() => ApplicationContext.Get("Key1"));
		ex.Message.ShouldContain("Circular reference detected");
	}

	[Fact]
	public void ThrowWhenPlaceholderCannotBeResolved()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "ApiUrl", "%Unknown%" } };
		ApplicationContext.Init(context);

		// Act & Assert
		var ex = Should.Throw<InvalidConfigurationException>(() => ApplicationContext.Get("ApiUrl"));

		ex.Message.ShouldContain("Unresolved placeholder", Case.Insensitive);
		ex.Message.ShouldContain("Unknown", Case.Insensitive);
	}

	[Fact]
	public void ExpandMixedEnvironmentAndContextPlaceholders()
	{
		// Arrange
		Environment.SetEnvironmentVariable("HOST", "env-host.com");
		var context = new Dictionary<string, string?> { { "PORT", "8080" }, { "Url", "http://%HOST%:%PORT%" } };
		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Expand("%Url%");

		// Assert
		result.ShouldBe("http://env-host.com:8080");

		// Cleanup
		Environment.SetEnvironmentVariable("HOST", null);
	}

	[Fact]
	public void EnvironmentVariableOverridesContextIfPresent()
	{
		// Arrange
		Environment.SetEnvironmentVariable("ServiceAccountName", "env-account");
		var context = new Dictionary<string, string?> { { "ServiceAccountName", "context-account" } };
		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Get("ServiceAccountName");

		// Assert
		result.ShouldBe("env-account");

		// Cleanup
		Environment.SetEnvironmentVariable("ServiceAccountName", null);
	}

	[Fact]
	public void ContextUsedWhenEnvironmentVariableIsMissing()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "ServiceAccountName", "context-account" } };
		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Get("ServiceAccountName");

		// Assert
		result.ShouldBe("context-account");
	}

	[Fact]
	public void ExpandHandlesMultipleOccurrencesOfSamePlaceholder()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "Token", "abc123" }, { "AuthHeader", "Bearer %Token% - %Token%" } };
		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Expand("%AuthHeader%");

		// Assert
		result.ShouldBe("Bearer abc123 - abc123");
	}

	[Fact]
	public void ExpandHandlesNestedPlaceholdersInContext()
	{
		// Arrange
		var context = new Dictionary<string, string?>
		{
			{ "Host", "localhost" },
			{ "Port", "5000" },
			{ "BaseUrl", "http://%Host%:%Port%" },
			{ "ServiceEndpoint", "%BaseUrl%/api/service" }
		};
		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Expand("%ServiceEndpoint%");

		// Assert
		result.ShouldBe("http://localhost:5000/api/service");
	}

	[Fact]
	public void GetReturnsUnexpandedStringIfValueContainsInvalidPlaceholderSyntax()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "Invalid", "This is not a %valid placeholder" } };
		ApplicationContext.Init(context);

		// Act
		var result = ApplicationContext.Get("Invalid");

		// Assert
		result.ShouldBe("This is not a %valid placeholder");
	}

	[Fact]
	public void ReturnDefaultContextAsLocalIfNotSet()
	{
		// Act
		var result = ApplicationContext.Context;

		// Assert
		result.ShouldBe("local");
	}

	[Fact]
	public void ReturnConfiguredContextIfSet()
	{
		// Arrange
		var context = new Dictionary<string, string?> { { "APP_CONTEXT_NAME", "TestContext" } };

		ApplicationContext.Init(context);
		Environment.SetEnvironmentVariable("TestContext", "production");

		// Act
		var result = ApplicationContext.Context;

		// Assert
		result.ShouldBe("production");

		// Cleanup
		Environment.SetEnvironmentVariable("TestContext", null);
	}

	[Fact]
	public void ReturnNullForExpandIfInputIsNull()
	{
		// Act
		var result = ApplicationContext.Expand(null);

		// Assert
		result.ShouldBeNull();
	}

	protected virtual async ValueTask DisposeAsyncCore()
	{
		ApplicationContextMother.Reset();
	}
}
