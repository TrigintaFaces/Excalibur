using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for CachedValue functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CachedValueShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaultValues_HasExpectedDefaults()
	{
		// Arrange & Act
		var cachedValue = new CachedValue();

		// Assert
		cachedValue.Value.ShouldBeNull();
		cachedValue.ShouldCache.ShouldBeFalse();
		cachedValue.HasExecuted.ShouldBeFalse();
		cachedValue.TypeName.ShouldBeNull();
	}

	[Fact]
	public void Create_WithValue_StoresValue()
	{
		// Arrange & Act
		var cachedValue = new CachedValue
		{
			Value = "test-value",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = "System.String"
		};

		// Assert
		cachedValue.Value.ShouldBe("test-value");
		cachedValue.ShouldCache.ShouldBeTrue();
		cachedValue.HasExecuted.ShouldBeTrue();
		cachedValue.TypeName.ShouldBe("System.String");
	}

	[Fact]
	public void Create_WithComplexValue_StoresReference()
	{
		// Arrange
		var complexObject = new { Name = "Test", Count = 42 };

		// Act
		var cachedValue = new CachedValue
		{
			Value = complexObject,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = complexObject.GetType().AssemblyQualifiedName
		};

		// Assert
		cachedValue.Value.ShouldBeSameAs(complexObject);
		cachedValue.TypeName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Create_WithNullValue_AllowsNullStorage()
	{
		// Arrange & Act
		var cachedValue = new CachedValue
		{
			Value = null,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = null
		};

		// Assert
		cachedValue.Value.ShouldBeNull();
		cachedValue.ShouldCache.ShouldBeTrue();
	}
}
