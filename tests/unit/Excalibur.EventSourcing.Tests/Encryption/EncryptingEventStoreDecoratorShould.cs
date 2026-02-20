// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Encryption.Decorators;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptingEventStoreDecorator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingEventStoreDecoratorShould
{
	private readonly IEventStore _innerStore;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly CancellationToken _ct = CancellationToken.None;

	public EncryptingEventStoreDecoratorShould()
	{
		_innerStore = A.Fake<IEventStore>();
		_registry = A.Fake<IEncryptionProviderRegistry>();
	}

	private EncryptingEventStoreDecorator CreateDecorator(EncryptionMode mode = EncryptionMode.EncryptAndDecrypt)
	{
		var options = Options.Create(new EncryptionOptions
		{
			Mode = mode,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1"
		});
		return new EncryptingEventStoreDecorator(_innerStore, _registry, options);
	}

	private static StoredEvent CreatePlaintextStoredEvent(string eventId, byte[] data, byte[]? metadata = null)
	{
		return new StoredEvent(eventId, "agg-1", "Order", "OrderCreated", data, metadata, 1, DateTimeOffset.UtcNow, false);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenInnerStoreIsNull()
	{
		var options = Options.Create(new EncryptionOptions());
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(null!, _registry, options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRegistryIsNull()
	{
		var options = Options.Create(new EncryptionOptions());
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(_innerStore, null!, options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingEventStoreDecorator(_innerStore, _registry, null!));
	}

	#endregion

	#region LoadAsync (no fromVersion) Tests

	[Fact]
	public async Task LoadAsync_ShouldReturnEventsUnchanged_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var events = new List<StoredEvent>
		{
			CreatePlaintextStoredEvent("evt-1", new byte[] { 1, 2, 3 })
		};
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(new byte[] { 1, 2, 3 });
	}

	[Fact]
	public async Task LoadAsync_ShouldPassthroughPlaintextEvents_WhenNotEncrypted()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 10, 20, 30 }; // Not magic bytes
		var events = new List<StoredEvent>
		{
			CreatePlaintextStoredEvent("evt-1", plainData)
		};
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
	}

	#endregion

	#region LoadAsync (with fromVersion) Tests

	[Fact]
	public async Task LoadAsyncFromVersion_ShouldDelegateToInnerStore()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var events = new List<StoredEvent>
		{
			CreatePlaintextStoredEvent("evt-1", new byte[] { 1, 2, 3 })
		};
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", 5L, _ct))
			.Returns(events);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", 5L, _ct);

		// Assert
		result.Count.ShouldBe(1);
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", 5L, _ct))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region AppendAsync Tests

	[Fact]
	public async Task AppendAsync_ShouldThrowInvalidOperation_WhenModeIsDecryptOnlyReadOnly()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.DecryptOnlyReadOnly);
		var events = new List<IDomainEvent>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await decorator.AppendAsync("agg-1", "Order", events, 0, _ct));
	}

	[Fact]
	public async Task AppendAsync_ShouldDelegateToInner_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var events = new List<IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);
		A.CallTo(() => _innerStore.AppendAsync("agg-1", "Order", events, 0, _ct))
			.Returns(expectedResult);

		// Act
		var result = await decorator.AppendAsync("agg-1", "Order", events, 0, _ct);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task AppendAsync_ShouldDelegateToInner_WhenModeIsDecryptOnlyWritePlaintext()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.DecryptOnlyWritePlaintext);
		var events = new List<IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);
		A.CallTo(() => _innerStore.AppendAsync("agg-1", "Order", events, 0, _ct))
			.Returns(expectedResult);

		// Act
		var result = await decorator.AppendAsync("agg-1", "Order", events, 0, _ct);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task AppendAsync_ShouldDelegateToInner_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var events = new List<IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(1, 0);
		A.CallTo(() => _innerStore.AppendAsync("agg-1", "Order", events, 0, _ct))
			.Returns(expectedResult);

		// Act
		var result = await decorator.AppendAsync("agg-1", "Order", events, 0, _ct);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task AppendAsync_ShouldDelegateToInner_WhenModeIsEncryptNewDecryptAll()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptNewDecryptAll);
		var events = new List<IDomainEvent>();
		var expectedResult = AppendResult.CreateSuccess(2, 0);
		A.CallTo(() => _innerStore.AppendAsync("agg-1", "Order", events, 0, _ct))
			.Returns(expectedResult);

		// Act
		var result = await decorator.AppendAsync("agg-1", "Order", events, 0, _ct);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion

	#region GetUndispatchedEventsAsync Tests

	[Fact]
	public async Task GetUndispatchedEventsAsync_ShouldReturnUnchanged_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var events = new List<StoredEvent>
		{
			CreatePlaintextStoredEvent("evt-1", new byte[] { 5, 6, 7 })
		};
		A.CallTo(() => _innerStore.GetUndispatchedEventsAsync(10, _ct))
			.Returns(events);

		// Act
		var result = await decorator.GetUndispatchedEventsAsync(10, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(new byte[] { 5, 6, 7 });
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_ShouldDelegateToInner()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		A.CallTo(() => _innerStore.GetUndispatchedEventsAsync(25, _ct))
			.Returns(new List<StoredEvent>());

		// Act
		var result = await decorator.GetUndispatchedEventsAsync(25, _ct);

		// Assert
		result.Count.ShouldBe(0);
		A.CallTo(() => _innerStore.GetUndispatchedEventsAsync(25, _ct))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region MarkEventAsDispatchedAsync Tests

	[Fact]
	public async Task MarkEventAsDispatchedAsync_ShouldDelegateToInner()
	{
		// Arrange
		var decorator = CreateDecorator();

		// Act
		await decorator.MarkEventAsDispatchedAsync("evt-1", _ct);

		// Assert
		A.CallTo(() => _innerStore.MarkEventAsDispatchedAsync("evt-1", _ct))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Decrypt with Null Metadata Tests

	[Fact]
	public async Task LoadAsync_ShouldHandleNullMetadata()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var events = new List<StoredEvent>
		{
			new StoredEvent("evt-1", "agg-1", "Order", "OrderCreated", new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, false)
		};
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].Metadata.ShouldBeNull();
	}

	#endregion

	#region Multiple Events Test

	[Fact]
	public async Task LoadAsync_ShouldHandleMultiplePlaintextEvents()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var events = new List<StoredEvent>
		{
			CreatePlaintextStoredEvent("evt-1", new byte[] { 1, 2, 3 }),
			CreatePlaintextStoredEvent("evt-2", new byte[] { 4, 5, 6 }),
			CreatePlaintextStoredEvent("evt-3", new byte[] { 7, 8, 9 })
		};
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(3);
	}

	#endregion
}
