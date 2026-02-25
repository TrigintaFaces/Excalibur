using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfigurationExceptionShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		var ex = new ConfigurationException();

		ex.ErrorCode.ShouldBe(ErrorCodes.ConfigurationInvalid);
		ex.Category.ShouldBe(ErrorCategory.Configuration);
		ex.Severity.ShouldBe(ErrorSeverity.Critical);
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new ConfigurationException("bad config");

		ex.Message.ShouldBe("bad config");
	}

	[Fact]
	public void CreateWithMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ConfigurationException("bad config", inner);

		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateWithErrorCodeAndMessage()
	{
		var ex = new ConfigurationException(ErrorCodes.ConfigurationMissing, "missing");

		ex.ErrorCode.ShouldBe(ErrorCodes.ConfigurationMissing);
	}

	[Fact]
	public void CreateWithErrorCodeMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ConfigurationException(ErrorCodes.ConfigurationLoadFailed, "load failed", inner);

		ex.ErrorCode.ShouldBe(ErrorCodes.ConfigurationLoadFailed);
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateMissingConfigException()
	{
		var ex = ConfigurationException.Missing("ConnectionString");

		ex.ShouldBeOfType<ConfigurationException>();
		ex.Message.ShouldContain("ConnectionString");
		ex.Message.ShouldContain("missing");
		ex.DispatchStatusCode.ShouldBe(500);
	}

	[Fact]
	public void CreateInvalidConfigException()
	{
		var ex = ConfigurationException.Invalid("Timeout", -1, "Must be positive");

		ex.ShouldBeOfType<ConfigurationException>();
		ex.Message.ShouldContain("Timeout");
		ex.Message.ShouldContain("Must be positive");
		ex.DispatchStatusCode.ShouldBe(500);
	}

	[Fact]
	public void CreateSectionNotFoundException()
	{
		var ex = ConfigurationException.SectionNotFound("Dispatch:Transport");

		ex.ShouldBeOfType<ConfigurationException>();
		ex.Message.ShouldContain("Dispatch:Transport");
		ex.DispatchStatusCode.ShouldBe(500);
	}
}
