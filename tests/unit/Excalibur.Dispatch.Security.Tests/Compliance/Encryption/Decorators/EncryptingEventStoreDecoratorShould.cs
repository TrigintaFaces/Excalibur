// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Encryption.Decorators;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption.Decorators;

/// <summary>
/// Unit tests for <see cref="EncryptingEventStoreDecorator"/>.
/// </summary>
/// <remarks>
/// Per AD-254-2, these tests verify mixed-mode read support and encryption mode behaviors.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class EncryptingEventStoreDecoratorShould
{
	private readonly IEventStore _innerStore;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly Dispatch.Compliance.EncryptionOptions _options;

	public EncryptingEventStoreDecoratorShould()
	{
		_innerStore = A.Fake<IEventStore>();
		_registry = A.Fake<IEncryptionProviderRegistry>();
		_options = new Dispatch.Compliance.EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "EventStore",
			DefaultTenantId = "test-tenant"
		};
	}

	private EncryptingEventStoreDecorator CreateSut() =>
		new(_innerStore, _registry, Microsoft.Extensions.Options.Options.Create(_options));

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenInnerStoreIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(null!, _registry, Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(_innerStore, null!, Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(_innerStore, _registry, null!));
	}

	[Fact]
	public void CreateSuccessfully_WithValidParameters()
	{
		// Act
		var sut = CreateSut();

		// Assert
		_ = sut.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region LoadAsync Tests - Plaintext Data

	[Fact]
	public async Task LoadAsync_ReturnPlaintextData_WhenDataIsNotEncrypted()
	{
		// Arrange
		var plaintextData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
		var storedEvent = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: plaintextData,
			Metadata: null,
			Version: 1,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var sut = CreateSut();

		// Act
		var result = await sut.LoadAsync("agg-1", "TestAggregate", CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plaintextData);
	}

	[Fact]
	public async Task LoadAsync_ReturnEmptyList_WhenNoEventsExist()
	{
		// Arrange
		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var sut = CreateSut();

		// Act
		var result = await sut.LoadAsync("agg-1", "TestAggregate", CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion LoadAsync Tests - Plaintext Data

	#region LoadAsync with Version Tests

	[Fact]
	public async Task LoadAsync_WithVersion_DelegatesToInnerStore()
	{
		// Arrange
		var plaintextData = new byte[] { 0x01, 0x02, 0x03 };
		var storedEvent = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: plaintextData,
			Metadata: null,
			Version: 5,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", 3L, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var sut = CreateSut();

		// Act
		var result = await sut.LoadAsync("agg-1", "TestAggregate", 3L, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].Version.ShouldBe(5);
	}

	#endregion LoadAsync with Version Tests

	#region AppendAsync - Mode Tests

	[Fact]
	public async Task AppendAsync_ThrowInvalidOperationException_WhenModeIsDecryptOnlyReadOnly()
	{
		// Arrange
		_options.Mode = EncryptionMode.DecryptOnlyReadOnly;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsDisabled()
	{
		// Arrange
		_options.Mode = EncryptionMode.Disabled;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsDecryptOnlyWritePlaintext()
	{
		// Arrange
		_options.Mode = EncryptionMode.DecryptOnlyWritePlaintext;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		_options.Mode = EncryptionMode.EncryptAndDecrypt;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsEncryptNewDecryptAll()
	{
		// Arrange
		_options.Mode = EncryptionMode.EncryptNewDecryptAll;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion AppendAsync - Mode Tests

	#region GetUndispatchedEventsAsync Tests

	[Fact]
	public async Task GetUndispatchedEventsAsync_ReturnPlaintextEvents()
	{
		// Arrange
		var plaintextData = new byte[] { 0x01, 0x02, 0x03 };
		var storedEvent = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: plaintextData,
			Metadata: null,
			Version: 1,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		_ = A.CallTo(() => _innerStore.GetUndispatchedEventsAsync(10, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var sut = CreateSut();

		// Act
		var result = await sut.GetUndispatchedEventsAsync(10, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plaintextData);
	}

	#endregion GetUndispatchedEventsAsync Tests

	#region MarkEventAsDispatchedAsync Tests

	[Fact]
	public async Task MarkEventAsDispatchedAsync_DelegateToInnerStore()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		await sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _innerStore.MarkEventAsDispatchedAsync("evt-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion MarkEventAsDispatchedAsync Tests

	#region Disabled Mode Tests

	[Fact]
	public async Task LoadAsync_ReturnDataAsIs_WhenModeIsDisabled()
	{
		// Arrange
		_options.Mode = EncryptionMode.Disabled;

		// Even if data looks like it might be encrypted, disabled mode returns as-is
		var magicBytesData = new byte[] { 0x45, 0x58, 0x43, 0x52, 0x01, 0x02 }; // EXCR prefix
		var storedEvent = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: magicBytesData,
			Metadata: null,
			Version: 1,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var sut = CreateSut();

		// Act
		var result = await sut.LoadAsync("agg-1", "TestAggregate", CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(magicBytesData); // Not decrypted - passed through
	}

	#endregion Disabled Mode Tests
}
