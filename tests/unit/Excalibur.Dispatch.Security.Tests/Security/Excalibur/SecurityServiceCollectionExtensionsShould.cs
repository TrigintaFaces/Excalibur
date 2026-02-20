// Copyright (c) TrigintaFaces. All rights reserved.

using Excalibur.Security.Abstractions;

using Microsoft.Extensions.Configuration;

using Excalibur.Security;
namespace Excalibur.Dispatch.Security.Tests.Excalibur;

/// <summary>
/// Unit tests for <see cref="SecurityServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityServiceCollectionExtensionsShould
{
	[Fact]
	public void AddPasswordHasher_RegistersPasswordHasherAsService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPasswordHasher();
		var provider = services.BuildServiceProvider();

		// Assert
		var hasher = provider.GetService<IPasswordHasher>();
		_ = hasher.ShouldNotBeNull();
		_ = hasher.ShouldBeOfType<Argon2idPasswordHasher>();
	}

	[Fact]
	public void AddPasswordHasher_WithConfiguration_AppliesOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPasswordHasher(options =>
		{
			options.MemorySize = 32768;
			options.Iterations = 2;
			options.Version = 99;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<Argon2Options>>();
		options.Value.MemorySize.ShouldBe(32768);
		options.Value.Iterations.ShouldBe(2);
		options.Value.Version.ShouldBe(99);
	}

	[Fact]
	public void AddPasswordHasher_RegistersAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPasswordHasher();
		var provider = services.BuildServiceProvider();

		// Assert
		var hasher1 = provider.GetService<IPasswordHasher>();
		var hasher2 = provider.GetService<IPasswordHasher>();
		ReferenceEquals(hasher1, hasher2).ShouldBeTrue();
	}

	[Fact]
	public void AddPasswordHasher_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddPasswordHasher());
	}

	[Fact]
	public void AddPasswordHasher_WithNullConfigure_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<Argon2Options>? configure = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddPasswordHasher(configure));
	}

	[Fact]
	public void AddPasswordHasher_WithConfiguration_BindsFromSection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Argon2:MemorySize"] = "16384",
				["Argon2:Iterations"] = "5",
				["Argon2:Parallelism"] = "8",
				["Argon2:Version"] = "42",
			})
			.Build();

		// Act
		_ = services.AddPasswordHasher(configuration);
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<Argon2Options>>();
		options.Value.MemorySize.ShouldBe(16384);
		options.Value.Iterations.ShouldBe(5);
		options.Value.Parallelism.ShouldBe(8);
		options.Value.Version.ShouldBe(42);
	}

	[Fact]
	public void AddPasswordHasher_WithNullConfiguration_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		IConfiguration? configuration = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddPasswordHasher(configuration));
	}

	[Fact]
	public void AddPasswordHasher_ReturnsServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddPasswordHasher();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
