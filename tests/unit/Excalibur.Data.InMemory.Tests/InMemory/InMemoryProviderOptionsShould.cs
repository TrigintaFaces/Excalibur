using Excalibur.Data.InMemory;
namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for InMemoryProviderOptions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryProviderOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new InMemoryProviderOptions();

		// Assert
		options.Name.ShouldBeNull();
		options.ConnectionString.ShouldBe("InMemory");
		options.ConnectionTimeout.ShouldBe(30);
		options.CommandTimeout.ShouldBe(30);
		options.MaxRetryAttempts.ShouldBe(0);
		options.RetryDelayMilliseconds.ShouldBe(1000);
		options.EnableConnectionPooling.ShouldBeFalse();
		options.MaxPoolSize.ShouldBe(100);
		options.MinPoolSize.ShouldBe(0);
		options.EnableDetailedLogging.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.MaxItemsPerCollection.ShouldBe(10000);
		options.PersistToDisk.ShouldBeFalse();
		options.PersistenceFilePath.ShouldBeNull();
		options.IsReadOnly.ShouldBeFalse();
	}

	[Fact]
	public void ProviderSpecificOptions_IsInitialized()
	{
		// Arrange & Act
		var options = new InMemoryProviderOptions();

		// Assert
		_ = options.ProviderSpecificOptions.ShouldNotBeNull();
		options.ProviderSpecificOptions.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_WithValidDefaults_DoesNotThrow()
	{
		// Arrange
		var options = new InMemoryProviderOptions();

		// Act & Assert
		Should.NotThrow(options.Validate);
	}

	[Fact]
	public void Validate_WithZeroMaxItems_ThrowsArgumentException()
	{
		// Arrange
		var options = new InMemoryProviderOptions
		{
			MaxItemsPerCollection = 0
		};

		// Act & Assert
		Should.Throw<ArgumentException>(options.Validate)
			.Message.ShouldContain("MaxItemsPerCollection");
	}

	[Fact]
	public void Validate_WithNegativeMaxItems_ThrowsArgumentException()
	{
		// Arrange
		var options = new InMemoryProviderOptions
		{
			MaxItemsPerCollection = -1
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(options.Validate);
	}

	[Fact]
	public void Validate_PersistToDiskWithoutPath_ThrowsArgumentException()
	{
		// Arrange
		var options = new InMemoryProviderOptions
		{
			PersistToDisk = true,
			PersistenceFilePath = null
		};

		// Act & Assert
		Should.Throw<ArgumentException>(options.Validate)
			.Message.ShouldContain("PersistenceFilePath");
	}

	[Fact]
	public void Validate_PersistToDiskWithPath_DoesNotThrow()
	{
		// Arrange
		var options = new InMemoryProviderOptions
		{
			PersistToDisk = true,
			PersistenceFilePath = "/tmp/data.json"
		};

		// Act & Assert
		Should.NotThrow(options.Validate);
	}

	[Fact]
	public void MaxItemsPerCollection_CanBeCustomized()
	{
		// Arrange & Act
		var options = new InMemoryProviderOptions
		{
			MaxItemsPerCollection = 50000
		};

		// Assert
		options.MaxItemsPerCollection.ShouldBe(50000);
	}
}
