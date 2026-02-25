namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckStorageOptionsShould
{
	[Fact]
	public void Have_correct_defaults()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
		options.ContainerName.ShouldBe("claim-checks");
		options.BlobNamePrefix.ShouldBe("claims");
		options.UseHierarchicalStorage.ShouldBeFalse();
		options.ColdStorageThreshold.ShouldBe(TimeSpan.FromDays(30));
		options.EnableEncryption.ShouldBeFalse();
		options.ChunkSize.ShouldBe(1024 * 1024);
		options.MaxConcurrency.ShouldBe(Environment.ProcessorCount);
		options.BufferPoolSize.ShouldBe(100);
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.MaxRetries.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Allow_custom_connection_string()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions { ConnectionString = "Server=test" };

		// Assert
		options.ConnectionString.ShouldBe("Server=test");
	}

	[Fact]
	public void Allow_custom_container_name()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions { ContainerName = "my-container" };

		// Assert
		options.ContainerName.ShouldBe("my-container");
	}

	[Fact]
	public void Allow_enabling_encryption()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions { EnableEncryption = true };

		// Assert
		options.EnableEncryption.ShouldBeTrue();
	}

	[Fact]
	public void Allow_enabling_hierarchical_storage()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions { UseHierarchicalStorage = true };

		// Assert
		options.UseHierarchicalStorage.ShouldBeTrue();
	}

	[Fact]
	public void Allow_custom_chunk_size()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions { ChunkSize = 512 * 1024 };

		// Assert
		options.ChunkSize.ShouldBe(512 * 1024);
	}

	[Fact]
	public void Allow_custom_max_concurrency()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions { MaxConcurrency = 8 };

		// Assert
		options.MaxConcurrency.ShouldBe(8);
	}

	[Fact]
	public void Allow_custom_operation_timeout()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions { OperationTimeout = TimeSpan.FromMinutes(2) };

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
