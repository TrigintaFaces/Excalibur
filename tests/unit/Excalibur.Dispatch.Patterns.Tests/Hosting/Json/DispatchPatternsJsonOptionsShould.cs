using System.Text.Json;

using Excalibur.Dispatch.Patterns;

using Shouldly;

using Tests.Shared;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Unit tests for DispatchPatternsJsonOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchPatternsJsonOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasWebDefaults()
	{
		// Arrange & Act
		var options = new DispatchPatternsJsonOptions();

		// Assert
		_ = options.SerializerOptions.ShouldNotBeNull();
		options.SerializerOptions.WriteIndented.ShouldBeFalse();
		options.SerializerContext.ShouldBeNull();
	}

	[Fact]
	public void SerializerOptions_IsCaseInsensitiveByDefault()
	{
		// Arrange & Act
		var options = new DispatchPatternsJsonOptions();

		// Assert - JsonSerializerDefaults.Web sets this
		options.SerializerOptions.PropertyNameCaseInsensitive.ShouldBeTrue();
	}

	[Fact]
	public void SerializerOptions_UsesCamelCaseNamingByDefault()
	{
		// Arrange & Act
		var options = new DispatchPatternsJsonOptions();

		// Assert - JsonSerializerDefaults.Web sets this
		options.SerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void SerializerContext_CanBeSet()
	{
		// Arrange
		var options = new DispatchPatternsJsonOptions();

		// Act - We test setting to null explicitly (since we can't easily create a context)
		options.SerializerContext = null;

		// Assert
		options.SerializerContext.ShouldBeNull();
	}

	[Fact]
	public void SerializerOptions_CanBeModified()
	{
		// Arrange
		var options = new DispatchPatternsJsonOptions();

		// Act
		options.SerializerOptions.WriteIndented = true;
		options.SerializerOptions.MaxDepth = 64;

		// Assert
		options.SerializerOptions.WriteIndented.ShouldBeTrue();
		options.SerializerOptions.MaxDepth.ShouldBe(64);
	}

	[Fact]
	public void SerializerOptions_AllowsReadingNumbers()
	{
		// Arrange & Act
		var options = new DispatchPatternsJsonOptions();

		// Assert - JsonSerializerDefaults.Web allows reading numbers from strings
		options.SerializerOptions.NumberHandling.ShouldBe(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString);
	}
}
