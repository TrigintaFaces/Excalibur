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
[Trait(TraitNames.Component, TestComponents.Compliance)]
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
		_ = Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(null!, _registry, Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRegistryIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(_innerStore, null!, Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(_innerStore, _registry, null!));
	}

	[Fact]
	public void CreateSuccessfully_WithValidParameters()
	{
		var sut = CreateSut();
		_ = sut.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region LoadAsync Tests - Plaintext Data

	[Fact]
	public async Task LoadAsync_ReturnPlaintextData_WhenDataIsNotEncrypted()
	{
		var plaintextData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
		var storedEvent = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: plaintextData,
			Metadata: null,
			Version: 1,
			Timestamp: DateTimeOffset.UtcNow);

		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var sut = CreateSut();

		var result = await sut.LoadAsync("agg-1", "TestAggregate", CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plaintextData);
	}

	[Fact]
	public async Task LoadAsync_ReturnEmptyList_WhenNoEventsExist()
	{
		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var sut = CreateSut();

		var result = await sut.LoadAsync("agg-1", "TestAggregate", CancellationToken.None);

		result.ShouldBeEmpty();
	}

	#endregion LoadAsync Tests - Plaintext Data

	#region LoadAsync with Version Tests

	[Fact]
	public async Task LoadAsync_WithVersion_DelegatesToInnerStore()
	{
		var plaintextData = new byte[] { 0x01, 0x02, 0x03 };
		var storedEvent = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: plaintextData,
			Metadata: null,
			Version: 5,
			Timestamp: DateTimeOffset.UtcNow);

		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", 3L, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var sut = CreateSut();

		var result = await sut.LoadAsync("agg-1", "TestAggregate", 3L, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].Version.ShouldBe(5);
	}

	#endregion LoadAsync with Version Tests

	#region AppendAsync - Mode Tests

	[Fact]
	public async Task AppendAsync_ThrowInvalidOperationException_WhenModeIsDecryptOnlyReadOnly()
	{
		_options.Mode = EncryptionMode.DecryptOnlyReadOnly;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();

		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsDisabled()
	{
		_options.Mode = EncryptionMode.Disabled;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		result.ShouldBe(expectedResult);
		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsDecryptOnlyWritePlaintext()
	{
		_options.Mode = EncryptionMode.DecryptOnlyWritePlaintext;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsEncryptAndDecrypt()
	{
		_options.Mode = EncryptionMode.EncryptAndDecrypt;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task AppendAsync_DelegateToInnerStore_WhenModeIsEncryptNewDecryptAll()
	{
		_options.Mode = EncryptionMode.EncryptNewDecryptAll;
		var sut = CreateSut();
		var events = new List<Dispatch.Abstractions.IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);

		_ = A.CallTo(() => _innerStore.AppendAsync("agg-1", "TestAggregate", events, 0L, A<CancellationToken>._))
			.Returns(expectedResult);

		var result = await sut.AppendAsync("agg-1", "TestAggregate", events, 0, CancellationToken.None);

		result.ShouldBe(expectedResult);
	}

	#endregion AppendAsync - Mode Tests

	#region Disabled Mode Tests

	[Fact]
	public async Task LoadAsync_ReturnDataAsIs_WhenModeIsDisabled()
	{
		_options.Mode = EncryptionMode.Disabled;

		var magicBytesData = new byte[] { 0x45, 0x58, 0x43, 0x52, 0x01, 0x02 }; // EXCR prefix
		var storedEvent = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: magicBytesData,
			Metadata: null,
			Version: 1,
			Timestamp: DateTimeOffset.UtcNow);

		_ = A.CallTo(() => _innerStore.LoadAsync("agg-1", "TestAggregate", A<CancellationToken>._))
			.Returns(new List<StoredEvent> { storedEvent });

		var sut = CreateSut();

		var result = await sut.LoadAsync("agg-1", "TestAggregate", CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(magicBytesData); // Not decrypted - passed through
	}

	#endregion Disabled Mode Tests
}
