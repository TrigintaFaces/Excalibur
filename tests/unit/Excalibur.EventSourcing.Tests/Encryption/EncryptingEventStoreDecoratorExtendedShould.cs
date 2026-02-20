// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

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
/// Extended unit tests for <see cref="EncryptingEventStoreDecorator"/> covering
/// encrypted data decryption paths, metadata handling, and all encryption modes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingEventStoreDecoratorExtendedShould
{
	private readonly IEventStore _innerStore;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IEncryptionProvider _provider;
	private readonly CancellationToken _ct = CancellationToken.None;

	public EncryptingEventStoreDecoratorExtendedShould()
	{
		_innerStore = A.Fake<IEventStore>();
		_registry = A.Fake<IEncryptionProviderRegistry>();
		_provider = A.Fake<IEncryptionProvider>();
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

	/// <summary>
	/// Creates encrypted event data with the EXCR magic bytes prefix followed by a valid JSON envelope.
	/// </summary>
	private static byte[] CreateEncryptedBytes(byte[] ciphertext, string keyId = "key-1", int keyVersion = 1)
	{
		var envelope = new EncryptedData
		{
			Ciphertext = ciphertext,
			KeyId = keyId,
			KeyVersion = keyVersion,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }
		};

		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(envelope);
		var magicBytes = new byte[] { 0x45, 0x58, 0x43, 0x52 }; // EXCR
		var result = new byte[magicBytes.Length + jsonBytes.Length];
		magicBytes.CopyTo(result, 0);
		jsonBytes.CopyTo(result, magicBytes.Length);
		return result;
	}

	private static StoredEvent CreateStoredEvent(
		string eventId, byte[] data, byte[]? metadata = null, long version = 1)
	{
		return new StoredEvent(eventId, "agg-1", "Order", "OrderCreated",
			data, metadata, version, DateTimeOffset.UtcNow, false);
	}

	private void SetupDecryption(byte[] plainData)
	{
		A.CallTo(() => _registry.FindDecryptionProvider(A<EncryptedData>._))
			.Returns(_provider);
		A.CallTo(() => _provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, _ct))
			.Returns(Task.FromResult(plainData));
	}

	#region Decryption of Encrypted Event Data

	[Fact]
	public async Task LoadAsync_ShouldDecryptEncryptedEventData_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 10, 20, 30 };
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 99, 98, 97 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedBytes)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		SetupDecryption(plainData);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
		A.CallTo(() => _provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, _ct))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LoadAsync_ShouldDecryptEncryptedMetadata_WhenPresent()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainEventData = new byte[] { 1, 2, 3 }; // Not encrypted
		var plainMetadata = new byte[] { 40, 50, 60 };
		var encryptedMetadata = CreateEncryptedBytes(new byte[] { 77, 88, 99 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", plainEventData, encryptedMetadata)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		A.CallTo(() => _registry.FindDecryptionProvider(A<EncryptedData>._))
			.Returns(_provider);
		A.CallTo(() => _provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, _ct))
			.Returns(Task.FromResult(plainMetadata));

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainEventData);
		result[0].Metadata.ShouldBe(plainMetadata);
	}

	[Fact]
	public async Task LoadAsync_ShouldDecryptBothEventDataAndMetadata()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainEventData = new byte[] { 10, 20, 30 };
		var plainMetadata = new byte[] { 40, 50, 60 };
		var encryptedEventData = CreateEncryptedBytes(new byte[] { 11 });
		var encryptedMetadata = CreateEncryptedBytes(new byte[] { 22 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedEventData, encryptedMetadata)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		A.CallTo(() => _registry.FindDecryptionProvider(A<EncryptedData>._))
			.Returns(_provider);

		var callCount = 0;
		A.CallTo(() => _provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, _ct))
			.ReturnsLazily(() =>
			{
				callCount++;
				return Task.FromResult(callCount == 1 ? plainEventData : plainMetadata);
			});

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainEventData);
		result[0].Metadata.ShouldBe(plainMetadata);
		A.CallTo(() => _provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, _ct))
			.MustHaveHappened(2, Times.Exactly);
	}

	#endregion

	#region No Provider Found

	[Fact]
	public async Task LoadAsync_ShouldThrowEncryptionException_WhenNoDecryptionProviderFound()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 99, 98, 97 });
		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedBytes)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		A.CallTo(() => _registry.FindDecryptionProvider(A<EncryptedData>._))
			.Returns((IEncryptionProvider?)null);

		// Act & Assert
		await Should.ThrowAsync<EncryptionException>(async () =>
			await decorator.LoadAsync("agg-1", "Order", _ct));
	}

	#endregion

	#region LoadAsync with FromVersion - All Modes

	[Fact]
	public async Task LoadAsyncFromVersion_ShouldDecryptEncryptedEvents_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 10, 20, 30 };
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 99 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedBytes, version: 5)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", 4L, _ct))
			.Returns(events);
		SetupDecryption(plainData);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", 4L, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
	}

	[Fact]
	public async Task LoadAsyncFromVersion_ShouldReturnUnchanged_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var eventData = new byte[] { 1, 2, 3 };
		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", eventData, version: 5)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", 4L, _ct))
			.Returns(events);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", 4L, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(eventData);
	}

	[Fact]
	public async Task LoadAsyncFromVersion_ShouldPassthroughPlaintext_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 10, 20, 30 }; // No magic bytes
		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", plainData, version: 5)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", 4L, _ct))
			.Returns(events);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", 4L, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
		A.CallTo(() => _registry.FindDecryptionProvider(A<EncryptedData>._)).MustNotHaveHappened();
	}

	#endregion

	#region GetUndispatchedEventsAsync - Encryption Modes

	[Fact]
	public async Task GetUndispatchedEventsAsync_ShouldDecryptEvents_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 10, 20, 30 };
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 55 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedBytes)
		};

		A.CallTo(() => _innerStore.GetUndispatchedEventsAsync(10, _ct))
			.Returns(events);
		SetupDecryption(plainData);

		// Act
		var result = await decorator.GetUndispatchedEventsAsync(10, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_ShouldPassthroughPlaintext()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 5, 6, 7 }; // Not encrypted
		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", plainData)
		};

		A.CallTo(() => _innerStore.GetUndispatchedEventsAsync(10, _ct))
			.Returns(events);

		// Act
		var result = await decorator.GetUndispatchedEventsAsync(10, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
	}

	#endregion

	#region Mixed Encrypted and Plaintext Events

	[Fact]
	public async Task LoadAsync_ShouldHandleMixedEncryptedAndPlaintextEvents()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData1 = new byte[] { 1, 2, 3 };
		var plainData2 = new byte[] { 10, 20, 30 };
		var decryptedData = new byte[] { 40, 50, 60 };
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 99 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", plainData1, version: 1),
			CreateStoredEvent("evt-2", encryptedBytes, version: 2),
			CreateStoredEvent("evt-3", plainData2, version: 3)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		SetupDecryption(decryptedData);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(3);
		result[0].EventData.ShouldBe(plainData1);
		result[1].EventData.ShouldBe(decryptedData);
		result[2].EventData.ShouldBe(plainData2);
		A.CallTo(() => _provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, _ct))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region DecryptOnlyWritePlaintext and EncryptNewDecryptAll Modes on Load

	[Fact]
	public async Task LoadAsync_ShouldDecryptEvents_WhenModeIsDecryptOnlyWritePlaintext()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.DecryptOnlyWritePlaintext);
		var plainData = new byte[] { 10, 20, 30 };
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 99 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedBytes)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		SetupDecryption(plainData);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
	}

	[Fact]
	public async Task LoadAsync_ShouldDecryptEvents_WhenModeIsDecryptOnlyReadOnly()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.DecryptOnlyReadOnly);
		var plainData = new byte[] { 10, 20, 30 };
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 99 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedBytes)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		SetupDecryption(plainData);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
	}

	[Fact]
	public async Task LoadAsync_ShouldDecryptEvents_WhenModeIsEncryptNewDecryptAll()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptNewDecryptAll);
		var plainData = new byte[] { 10, 20, 30 };
		var encryptedBytes = CreateEncryptedBytes(new byte[] { 99 });

		var events = new List<StoredEvent>
		{
			CreateStoredEvent("evt-1", encryptedBytes)
		};

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(events);
		SetupDecryption(plainData);

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(plainData);
	}

	#endregion

	#region EncryptionOptions Context Propagation

	[Fact]
	public void Constructor_ShouldUseDefaultPurposeAndTenantIdFromOptions()
	{
		// Arrange & Act - ensure decorator initializes with FIPS compliance flag
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "special-purpose",
			DefaultTenantId = "tenant-42",
			RequireFipsCompliance = true
		});

		// Act - should not throw
		var decorator = new EncryptingEventStoreDecorator(_innerStore, _registry, options);

		// Assert
		decorator.ShouldNotBeNull();
	}

	#endregion

	#region Empty Events List

	[Fact]
	public async Task LoadAsync_ShouldReturnEmptyList_WhenNoEventsExist()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", _ct))
			.Returns(new List<StoredEvent>());

		// Act
		var result = await decorator.LoadAsync("agg-1", "Order", _ct);

		// Assert
		result.Count.ShouldBe(0);
	}

	#endregion
}
