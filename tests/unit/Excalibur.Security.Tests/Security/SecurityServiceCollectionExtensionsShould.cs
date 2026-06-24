// Copyright (c) TrigintaFaces. All rights reserved.

using Excalibur.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Tests.Security;

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

	[Fact]
	public void AddPasswordHasher_IsIdempotent()
	{
		// Arrange — calling AddPasswordHasher twice must not double-register (Bug #8)
		var services = new ServiceCollection();

		// Act
		services.AddPasswordHasher();
		services.AddPasswordHasher();

		// Assert — exactly one IPasswordHasher registration
		var descriptors = services
			.Where(d => d.ServiceType == typeof(IPasswordHasher))
			.ToList();
		descriptors.Count.ShouldBe(1,
			"TryAddSingleton must prevent duplicate IPasswordHasher registrations");
	}

	[Fact]
	public void AddSecurityAuditing_RegistersSecurityEventLoggerWithForwardingPattern()
	{
		// Arrange — the forwarding pattern fix (Bug #6): concrete type registered,
		// then ISecurityEventLogger forwards via factory delegate
		var services = new ServiceCollection();
		services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:StoreType"] = "MEMORY",
			})
			.Build();

		// Act
		services.AddSecurityAuditing(configuration);

		// Assert — ISecurityEventLogger uses a forwarding factory (not direct ImplementationType)
		var interfaceDescriptor = services
			.FirstOrDefault(d => d.ServiceType == typeof(ISecurityEventLogger));
		interfaceDescriptor.ShouldNotBeNull();
		interfaceDescriptor.ImplementationFactory.ShouldNotBeNull(
			"ISecurityEventLogger should use a forwarding factory to resolve from concrete type");
	}

	[Fact]
	public void AddSecurityAuditing_ResolvesSecurityEventLoggerFromContainer()
	{
		// Arrange — verify the forwarding pattern actually resolves at runtime (Bug #6)
		var services = new ServiceCollection();
		services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:StoreType"] = "MEMORY",
			})
			.Build();

		// Act
		services.AddSecurityAuditing(configuration);
		var provider = services.BuildServiceProvider();

		// Assert — resolving ISecurityEventLogger should succeed
		var logger = provider.GetService<ISecurityEventLogger>();
		logger.ShouldNotBeNull("ISecurityEventLogger should resolve from the forwarding registration");
	}

	[Fact]
	public void AddSecurityAuditing_IsIdempotent()
	{
		// Arrange — calling twice must not duplicate hosted services (Bug #7)
		var services = new ServiceCollection();
		services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:StoreType"] = "MEMORY",
			})
			.Build();

		// Act
		services.AddSecurityAuditing(configuration);
		services.AddSecurityAuditing(configuration);

		// Assert — exactly one ISecurityEventLogger forwarding
		var interfaceDescriptors = services
			.Where(d => d.ServiceType == typeof(ISecurityEventLogger))
			.ToList();
		interfaceDescriptors.Count.ShouldBe(1,
			"TryAddSingleton must prevent duplicate ISecurityEventLogger registrations");
	}

	[Fact]
	public void AddSecurityAuditing_EventStoreIsIdempotent()
	{
		// Arrange — calling twice must not duplicate event store registrations (Bug #8). Verified against
		// the default in-memory store: StoreType="SQL" now fails fast (bd-kitw4i — no SQL ISecurityEventStore
		// ships in Excalibur.Security, so it refuses to start rather than silently discard audit events;
		// that throw is covered by the kitw4i fail-fast lock). Idempotency is orthogonal to store type.
		var services = new ServiceCollection();
		services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		// Act
		services.AddSecurityAuditing(configuration);
		services.AddSecurityAuditing(configuration);

		// Assert — exactly one ISecurityEventStore registration
		var storeDescriptors = services
			.Where(d => d.ServiceType == typeof(ISecurityEventStore))
			.ToList();
		storeDescriptors.Count.ShouldBe(1,
			"TryAddSingleton must prevent duplicate ISecurityEventStore registrations");
	}

	[Fact]
	public void AddSecureCredentialManagement_IsIdempotent_WithoutVault()
	{
		// Arrange — calling twice without Vault must not duplicate base credential stores (Bug #8)
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		// Act
		services.AddSecureCredentialManagement(configuration);
		services.AddSecureCredentialManagement(configuration);

		// Assert — exactly one ICredentialStore registration (EnvironmentVariable only)
		var storeDescriptors = services
			.Where(d => d.ServiceType == typeof(ICredentialStore))
			.ToList();
		storeDescriptors.Count.ShouldBe(1,
			"TryAddSingleton must prevent duplicate ICredentialStore registrations");

		// Assert — exactly one ISecureCredentialProvider registration
		var providerDescriptors = services
			.Where(d => d.ServiceType == typeof(ISecureCredentialProvider))
			.ToList();
		providerDescriptors.Count.ShouldBe(1,
			"TryAddSingleton must prevent duplicate ISecureCredentialProvider registrations");
	}

	[Fact]
	public void AddSecureCredentialManagement_IsIdempotent_WithVault()
	{
		// Arrange — calling twice with Vault configured must not duplicate Vault stores
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Vault:Url"] = "https://vault.example.com:8200",
			})
			.Build();

		// Act
		services.AddSecureCredentialManagement(configuration);
		services.AddSecureCredentialManagement(configuration);

		// Assert — ICredentialStore: 1 EnvironmentVariable + 1 Vault = 2 total (multi-registration)
		var storeDescriptors = services
			.Where(d => d.ServiceType == typeof(ICredentialStore))
			.ToList();
		storeDescriptors.Count.ShouldBe(2,
			"ICredentialStore is multi-registration (env var + Vault), but duplicate calls must not create more");

		// Assert — exactly one IWritableCredentialStore (Vault only)
		var writableDescriptors = services
			.Where(d => d.ServiceType == typeof(IWritableCredentialStore))
			.ToList();
		writableDescriptors.Count.ShouldBe(1,
			"Duplicate calls must not double-register IWritableCredentialStore");
	}
}
