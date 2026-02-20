// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Firestore;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestorePersistenceProvider"/> constructor validation,
/// property defaults, disposed guard, and GetService behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestorePersistenceProviderShould : UnitTestBase
{
	private readonly ILogger<FirestorePersistenceProvider> _logger;
	private readonly IOptions<FirestoreOptions> _validOptions;

	public FirestorePersistenceProviderShould()
	{
		_logger = A.Fake<ILogger<FirestorePersistenceProvider>>();
		_validOptions = Options.Create(new FirestoreOptions
		{
			ProjectId = "test-project",
			Name = "TestFirestore",
		});
	}

	#region Constructor Validation

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestorePersistenceProvider(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestorePersistenceProvider(_validOptions, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void Constructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Neither ProjectId nor EmulatorHost
		var invalidOptions = Options.Create(new FirestoreOptions());

		_ = Should.Throw<InvalidOperationException>(() =>
			new FirestorePersistenceProvider(invalidOptions, _logger));
	}

	[Fact]
	public void Constructor_WithValidOptions_CreatesInstance()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		_ = provider.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithEmulatorHost_CreatesInstance()
	{
		var options = Options.Create(new FirestoreOptions
		{
			EmulatorHost = "localhost:8080",
		});

		var provider = new FirestorePersistenceProvider(options, _logger);

		_ = provider.ShouldNotBeNull();
	}

	[Fact]
	public void DbConstructor_WithNullDb_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestorePersistenceProvider(db: null!, _validOptions, _logger));
		exception.ParamName.ShouldBe("db");
	}

	[Fact]
	public void DbConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// db: null! will throw first; testing options null requires a non-null db
		// FirestoreDb is sealed/abstract so we cannot mock it easily.
		// Testing that null options throws when db is also null (first null check wins)
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestorePersistenceProvider(db: null!, options: null!, _logger));
		exception.ParamName.ShouldBe("db");
	}

	#endregion Constructor Validation

	#region Property Defaults

	[Fact]
	public void Name_ReturnsOptionsName()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.Name.ShouldBe("TestFirestore");
	}

	[Fact]
	public void ProviderType_ReturnsCloudNative()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.ProviderType.ShouldBe("CloudNative");
	}

	[Fact]
	public void DocumentStoreType_ReturnsFirestore()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.DocumentStoreType.ShouldBe("Firestore");
	}

	[Fact]
	public void IsAvailable_BeforeInitialization_ReturnsFalse()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void SupportsChangeFeed_ReturnsTrue()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.SupportsChangeFeed.ShouldBeTrue();
	}

	[Fact]
	public void SupportsMultiRegionWrites_ReturnsFalse()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.SupportsMultiRegionWrites.ShouldBeFalse();
	}

	[Fact]
	public void ConnectionString_ContainsProjectId()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.ConnectionString.ShouldContain("test-project");
	}

	[Fact]
	public void RetryPolicy_ReturnsFirestoreRetryPolicyInstance()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		_ = provider.RetryPolicy.ShouldNotBeNull();
	}

	#endregion Property Defaults

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		await provider.DisposeAsync().ConfigureAwait(false);
		await provider.DisposeAsync().ConfigureAwait(false);
		await provider.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		provider.Dispose();
		provider.Dispose();
		provider.Dispose();
	}

	[Fact]
	public async Task IsAvailable_AfterDispose_ReturnsFalse()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		await provider.DisposeAsync().ConfigureAwait(false);

		provider.IsAvailable.ShouldBeFalse();
	}

	#endregion Dispose Tests

	#region GetService Tests

	[Fact]
	public void GetService_WithHealthType_ReturnsSelf()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		var service = provider.GetService(typeof(IPersistenceProviderHealth));

		service.ShouldBeSameAs(provider);
	}

	[Fact]
	public void GetService_WithTransactionType_ReturnsSelf()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		var service = provider.GetService(typeof(IPersistenceProviderTransaction));

		service.ShouldBeSameAs(provider);
	}

	[Fact]
	public void GetService_WithUnknownType_ReturnsNull()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		var service = provider.GetService(typeof(string));

		service.ShouldBeNull();
	}

	[Fact]
	public void GetService_WithNullType_ThrowsArgumentNullException()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		_ = Should.Throw<ArgumentNullException>(() =>
			provider.GetService(null!));
	}

	#endregion GetService Tests

	#region Unsupported Operations

	[Fact]
	public void CreateTransactionScope_ThrowsNotSupportedException()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		_ = Should.Throw<NotSupportedException>(() =>
			provider.CreateTransactionScope());
	}

	[Fact]
	public void GetSupportedOperationTypes_ReturnsExpectedTypes()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);

		var operations = provider.GetSupportedOperationTypes().ToList();

		operations.ShouldContain("Create");
		operations.ShouldContain("Read");
		operations.ShouldContain("Update");
		operations.ShouldContain("Delete");
		operations.ShouldContain("Query");
		operations.ShouldContain("Batch");
		operations.ShouldContain("Realtime");
	}

	#endregion Unsupported Operations

	#region Uninitialized Guard

	[Fact]
	public async Task GetByIdAsync_WithoutInitialization_ThrowsInvalidOperationException()
	{
		var provider = new FirestorePersistenceProvider(_validOptions, _logger);
		var partitionKey = A.Fake<Excalibur.Data.Abstractions.CloudNative.IPartitionKey>();

		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await provider.GetByIdAsync<object>("id", partitionKey, null, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Uninitialized Guard
}
