// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbPersistenceProvider"/> constructor validation,
/// property defaults, disposed guard, and GetService behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbPersistenceProviderShould : UnitTestBase
{
	private readonly ILogger<DynamoDbPersistenceProvider> _logger;
	private readonly IOptions<DynamoDbOptions> _validOptions;

	public DynamoDbPersistenceProviderShould()
	{
		_logger = A.Fake<ILogger<DynamoDbPersistenceProvider>>();
		_validOptions = Options.Create(new DynamoDbOptions
		{
			ServiceUrl = "http://localhost:8000",
			Name = "TestDynamoDb",
		});
	}

	#region Constructor Validation

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbPersistenceProvider(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbPersistenceProvider(_validOptions, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void Constructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Neither ServiceUrl nor Region
		var invalidOptions = Options.Create(new DynamoDbOptions
		{
			ServiceUrl = null,
			Region = null,
		});

		_ = Should.Throw<InvalidOperationException>(() =>
			new DynamoDbPersistenceProvider(invalidOptions, _logger));
	}

	[Fact]
	public void Constructor_WithValidOptions_CreatesInstance()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		_ = provider.ShouldNotBeNull();
	}

	[Fact]
	public void ClientConstructor_WithNullClient_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbPersistenceProvider(client: null!, _validOptions, _logger));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void ClientConstructor_WithValidClient_CreatesInitializedProvider()
	{
		var fakeClient = A.Fake<IAmazonDynamoDB>();

		var provider = new DynamoDbPersistenceProvider(fakeClient, _validOptions, _logger);

		_ = provider.ShouldNotBeNull();
		provider.Client.ShouldBeSameAs(fakeClient);
	}

	#endregion Constructor Validation

	#region Property Defaults

	[Fact]
	public void Name_ReturnsOptionsName()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.Name.ShouldBe("TestDynamoDb");
	}

	[Fact]
	public void ProviderType_ReturnsCloudNative()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.ProviderType.ShouldBe("CloudNative");
	}

	[Fact]
	public void DocumentStoreType_ReturnsDynamoDB()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.DocumentStoreType.ShouldBe("DynamoDB");
	}

	[Fact]
	public void IsAvailable_BeforeInitialization_ReturnsFalse()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void SupportsChangeFeed_ReturnsTrue()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.SupportsChangeFeed.ShouldBeTrue();
	}

	[Fact]
	public void SupportsMultiRegionWrites_ReturnsTrue()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.SupportsMultiRegionWrites.ShouldBeTrue();
	}

	[Fact]
	public void ConnectionString_ContainsServiceUrl()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.ConnectionString.ShouldContain("localhost:8000");
	}

	[Fact]
	public void RetryPolicy_IsNotNull()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		_ = provider.RetryPolicy.ShouldNotBeNull();
	}

	#endregion Property Defaults

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		await provider.DisposeAsync().ConfigureAwait(false);
		await provider.DisposeAsync().ConfigureAwait(false);
		await provider.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		provider.Dispose();
		provider.Dispose();
		provider.Dispose();
	}

	[Fact]
	public async Task IsAvailable_AfterDispose_ReturnsFalse()
	{
		var provider = new DynamoDbPersistenceProvider(_validOptions, _logger);

		await provider.DisposeAsync().ConfigureAwait(false);

		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeAsync_WithClient_DisposesClient()
	{
		var fakeClient = A.Fake<IAmazonDynamoDB>();
		var provider = new DynamoDbPersistenceProvider(fakeClient, _validOptions, _logger);

		await provider.DisposeAsync().ConfigureAwait(false);

		A.CallTo(() => fakeClient.Dispose()).MustHaveHappenedOnceExactly();
	}

	#endregion Dispose Tests

	#region Disposed Guard

	[Fact]
	public async Task GetByIdAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		var fakeClient = A.Fake<IAmazonDynamoDB>();
		var provider = new DynamoDbPersistenceProvider(fakeClient, _validOptions, _logger);
		await provider.DisposeAsync().ConfigureAwait(false);

		var partitionKey = A.Fake<Excalibur.Data.Abstractions.CloudNative.IPartitionKey>();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await provider.GetByIdAsync<object>("id", partitionKey, null, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Disposed Guard

	#region GetService Tests

	[Fact]
	public void GetService_WithHealthType_ReturnsSelf()
	{
		var fakeClient = A.Fake<IAmazonDynamoDB>();
		var provider = new DynamoDbPersistenceProvider(fakeClient, _validOptions, _logger);

		var service = provider.GetService(typeof(IPersistenceProviderHealth));

		service.ShouldBeSameAs(provider);
	}

	[Fact]
	public void GetService_WithTransactionType_ReturnsSelf()
	{
		var fakeClient = A.Fake<IAmazonDynamoDB>();
		var provider = new DynamoDbPersistenceProvider(fakeClient, _validOptions, _logger);

		var service = provider.GetService(typeof(IPersistenceProviderTransaction));

		service.ShouldBeSameAs(provider);
	}

	[Fact]
	public void GetService_WithUnknownType_ReturnsNull()
	{
		var fakeClient = A.Fake<IAmazonDynamoDB>();
		var provider = new DynamoDbPersistenceProvider(fakeClient, _validOptions, _logger);

		var service = provider.GetService(typeof(string));

		service.ShouldBeNull();
	}

	#endregion GetService Tests
}
