namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ClaimCheckStorageOptionsShould
{
	/// <summary>
	/// The storage options must not advertise an encryption control, because nothing here can implement
	/// one honestly.
	/// </summary>
	/// <remarks>
	/// A bare boolean carried no key, no key provider and no algorithm, and no claim check store ever
	/// read it, so a consumer who set it believed stored payloads were encrypted at rest and they were
	/// not. It cannot be made to work from this assembly either: payload encryption needs key material,
	/// and this package deliberately depends on nothing that supplies any. Encryption for claim check
	/// payloads belongs to the store that holds the key — server-side encryption on the bucket or
	/// container, or a client configured with a key-encryption key — never to a flag in shared options.
	/// This asserts by shape rather than by name so the same mistake cannot return under a new spelling.
	/// </remarks>
	[Fact]
	public void Not_advertise_an_encryption_control()
	{
		var advertised = typeof(ClaimCheckStorageOptions)
			.GetProperties()
			.Select(property => property.Name)
			.Where(name => name.Contains("Encrypt", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		advertised.ShouldBeEmpty();
	}

	[Fact]
	public void Have_correct_defaults()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
		options.ContainerName.ShouldBe("claim-checks");
		options.BlobNamePrefix.ShouldBe("claims");
		options.Operations.MaxConcurrency.ShouldBe(Environment.ProcessorCount);
		options.Operations.BufferPoolSize.ShouldBe(100);
		options.Operations.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.Operations.MaxRetries.ShouldBe(3);
		options.Operations.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
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
	public void Allow_custom_max_concurrency()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions
		{
			Operations = new ClaimCheckOperationOptions { MaxConcurrency = 8 }
		};

		// Assert
		options.Operations.MaxConcurrency.ShouldBe(8);
	}

	[Fact]
	public void Allow_custom_operation_timeout()
	{
		// Arrange & Act
		var options = new ClaimCheckStorageOptions
		{
			Operations = new ClaimCheckOperationOptions { OperationTimeout = TimeSpan.FromMinutes(2) }
		};

		// Assert
		options.Operations.OperationTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
