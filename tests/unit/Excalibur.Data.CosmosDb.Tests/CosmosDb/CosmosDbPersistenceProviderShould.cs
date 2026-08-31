// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="CosmosDbPersistenceProvider"/> constructor validation,
/// property defaults, disposed guard, and GetService behavior.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class CosmosDbPersistenceProviderShould : UnitTestBase
{
	private readonly ILogger<CosmosDbPersistenceProvider> _logger;
	private readonly IOptions<CosmosDbOptions> _validOptions;

	public CosmosDbPersistenceProviderShould()
	{
		_logger = A.Fake<ILogger<CosmosDbPersistenceProvider>>();
		_validOptions = Options.Create(new CosmosDbOptions
		{
			Client = new() { AccountEndpoint = "https://localhost:8081", AccountKey = CreateNonSecretAccountKey() },
			DatabaseName = "TestDb",
			Name = "TestCosmosDb",
		});
	}

	#region Constructor Validation

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new CosmosDbPersistenceProvider(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new CosmosDbPersistenceProvider(_validOptions, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void Constructor_WithInvalidOptions_MissingConnectionInfo_ThrowsInvalidOperationException()
	{
		// Neither ConnectionString nor AccountEndpoint+AccountKey
		var invalidOptions = Options.Create(new CosmosDbOptions
		{
			DatabaseName = "TestDb",
		});

		_ = Should.Throw<InvalidOperationException>(() =>
			new CosmosDbPersistenceProvider(invalidOptions, _logger));
	}

	[Fact]
	public void Constructor_WithInvalidOptions_MissingDatabaseName_ThrowsInvalidOperationException()
	{
		var invalidOptions = Options.Create(new CosmosDbOptions
		{
			Client = new() { AccountEndpoint = "https://localhost:8081", AccountKey = CreateNonSecretAccountKey() },
			DatabaseName = null,
		});

		_ = Should.Throw<InvalidOperationException>(() =>
			new CosmosDbPersistenceProvider(invalidOptions, _logger));
	}

	[Fact]
	public void Constructor_WithValidOptions_CreatesInstance()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		_ = provider.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithConnectionString_CreatesInstance()
	{
		var options = Options.Create(new CosmosDbOptions
		{
			Client = new() { ConnectionString = BuildLocalConnectionString() },
			DatabaseName = "TestDb",
		});

		var provider = new CosmosDbPersistenceProvider(options, _logger);

		_ = provider.ShouldNotBeNull();
	}

	private static string CreateNonSecretAccountKey()
	{
		return string.Concat("local-", "cosmos-", "fixture-", "key");
	}

	private static string BuildLocalConnectionString()
	{
		return string.Concat("AccountEndpoint=https://localhost:8081/;AccountKey=", CreateNonSecretAccountKey());
	}

	#endregion Constructor Validation

	#region Property Defaults

	[Fact]
	public void Name_ReturnsOptionsName()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.Name.ShouldBe("TestCosmosDb");
	}

	[Fact]
	public void ProviderType_ReturnsCloudNative()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.ProviderType.ShouldBe("CloudNative");
	}

	[Fact]
	public void DocumentStoreType_ReturnsCosmosDB()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.DocumentStoreType.ShouldBe("CosmosDB");
	}

	[Fact]
	public void IsAvailable_BeforeInitialization_ReturnsFalse()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void SupportsChangeFeed_ReturnsTrue()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.SupportsChangeFeed.ShouldBeTrue();
	}

	[Fact]
	public void SupportsMultiRegionWrites_ReturnsTrue()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.SupportsMultiRegionWrites.ShouldBeTrue();
	}

	[Fact]
	public void CloudProvider_ReturnsCosmosDb()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.CloudProvider.ShouldBe(Excalibur.Data.CloudNative.CloudPersistenceProviderType.CosmosDb);
	}

	[Fact]
	public void Client_BeforeInitialization_ReturnsNull()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.Client.ShouldBeNull();
	}

	[Fact]
	public void Database_BeforeInitialization_ReturnsNull()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.Database.ShouldBeNull();
	}

	#endregion Property Defaults

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		await provider.DisposeAsync().ConfigureAwait(false);
		await provider.DisposeAsync().ConfigureAwait(false);
		await provider.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		provider.Dispose();
		provider.Dispose();
		provider.Dispose();
	}

	[Fact]
	public async Task IsAvailable_AfterDispose_ReturnsFalse()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		await provider.DisposeAsync().ConfigureAwait(false);

		provider.IsAvailable.ShouldBeFalse();
	}

	#endregion Dispose Tests

	#region Disposed Guard

	[Fact]
	public async Task GetByIdAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);
		await provider.DisposeAsync().ConfigureAwait(false);

		var partitionKey = A.Fake<Excalibur.Data.CloudNative.IPartitionKey>();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await provider.GetByIdAsync<object>("id", partitionKey, null, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Disposed Guard

	#region GetService Tests

	[Fact]
	public void GetService_WithHealthType_ReturnsSelf()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		var service = provider.GetService(typeof(IPersistenceProviderHealth));

		service.ShouldBeSameAs(provider);
	}

	[Fact]
	public void GetService_WithTransactionType_ReturnsNull()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		var service = provider.GetService(typeof(IPersistenceProviderTransaction));

		service.ShouldBeNull(
			"Cosmos DB cannot honour an ambient transaction scope, so it declines the capability at "
			+ "discovery. Advertising it and throwing at the point of use tells the caller only after "
			+ "they have committed to a design that cannot work.");
	}

	[Fact]
	public void GetService_WithConnectionType_ReturnsSelf()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		var service = provider.GetService(typeof(IPersistenceProviderConnection));

		service.ShouldBeSameAs(provider);
	}

	[Fact]
	public void GetService_WithUnknownType_ReturnsNull()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		var service = provider.GetService(typeof(string));

		service.ShouldBeNull();
	}

	[Fact]
	public void GetService_WithNullType_ThrowsArgumentNullException()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);

		_ = Should.Throw<ArgumentNullException>(() =>
			provider.GetService(null!));
	}

	#endregion GetService Tests

	#region Self-Initialization

	[Fact]
	public async Task GetByIdAsync_WithoutAnExplicitInitializeCall_InitializesItselfInsteadOfRefusing()
	{
		var provider = new CosmosDbPersistenceProvider(_validOptions, _logger);
		var partitionKey = A.Fake<Excalibur.Data.CloudNative.IPartitionKey>();
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync().ConfigureAwait(false);

		// This is the constructor the provider's own DI extension resolves, and nothing in the framework
		// drives InitializeAsync, so an operation has to initialize on its own or it can never run at all.
		// An already-cancelled token proves it reaches initialization without needing Cosmos: the
		// first thing initialization awaits is the token, so cancellation is what surfaces. The previous
		// behaviour refused with InvalidOperationException before ever looking at the token, which is the
		// signature of an operation that never started.
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await provider.GetByIdAsync<object>("id", partitionKey, null, cancelled.Token)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Self-Initialization
}
