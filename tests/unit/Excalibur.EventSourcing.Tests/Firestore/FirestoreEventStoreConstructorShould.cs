// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.Firestore;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Firestore;

/// <summary>
/// Unit tests for the <see cref="FirestoreEventStore"/> dual-constructor pattern.
/// Verifies both simple (options-based) and advanced (FirestoreDb) constructors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreEventStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<FirestoreEventStore> _logger;
	private readonly IOptions<FirestoreEventStoreOptions> _validOptions;

	public FirestoreEventStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<FirestoreEventStore>>();
		_validOptions = Options.Create(new FirestoreEventStoreOptions
		{
			ProjectId = "test-project",
			EventsCollectionName = "events"
		});
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Arrange & Act
		var store = new FirestoreEventStore(_validOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreEventStore(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreEventStore(_validOptions, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Simple Constructor Tests

	#region FirestoreDb Constructor Tests

	[Fact]
	public void DbConstructor_WithNullDb_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreEventStore(db: null!, _validOptions, _logger));
		exception.ParamName.ShouldBe("db");
	}

	[Fact]
	public void DbConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert - This will throw before reaching the options null check
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreEventStore(db: null!, options: null!, _logger));
		exception.ParamName.ShouldBe("db");
	}

	[Fact]
	public void DbConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreEventStore(db: null!, _validOptions, logger: null!));
		exception.ParamName.ShouldBe("db");
	}

	#endregion FirestoreDb Constructor Tests

	#region CloudProviderType Tests

	[Fact]
	public void ProviderType_ReturnsFirestore()
	{
		// Arrange
		var store = new FirestoreEventStore(_validOptions, _logger);

		// Act & Assert
		store.ProviderType.ShouldBe(CloudProviderType.Firestore);
	}

	#endregion CloudProviderType Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new FirestoreEventStore(_validOptions, _logger);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	#endregion Dispose Tests
}
