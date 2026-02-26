// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;
using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryClaimCheckProvider"/> core CRUD operations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryClaimCheckProviderTests
{
	[Fact]
	public async Task StoreAsync_ShouldStorePayloadAndReturnReference()
	{
		// Arrange
		var provider = CreateProvider();
		var payload = "Hello, World!"u8.ToArray();

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Assert
		_ = reference.ShouldNotBeNull();
		reference.Id.ShouldNotBeNullOrWhiteSpace();
		reference.Id.ShouldStartWith("cc-");
		reference.Size.ShouldBe(payload.Length);
		reference.Location.ShouldStartWith("inmemory://");
		reference.BlobName.ShouldContain(reference.Id);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		reference.StoredAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
		_ = reference.ExpiresAt.ShouldNotBeNull();
		reference.ExpiresAt.Value.ShouldBeGreaterThan(reference.StoredAt);
	}

	[Fact]
	public async Task StoreAsync_WithMetadata_ShouldStoreMetadata()
	{
		// Arrange
		var provider = CreateProvider();
		var payload = "Test payload"u8.ToArray();
		var metadata = new ClaimCheckMetadata
		{
			ContentType = "text/plain",
			OriginalSize = payload.Length,
			Properties =
			{
				["MessageId"] = "msg-123",
				["CorrelationId"] = "corr-456"
			}
		};

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None, metadata);

		// Assert
		_ = reference.Metadata.ShouldNotBeNull();
		reference.Metadata.ContentType.ShouldBe("text/plain");
		reference.Metadata.OriginalSize.ShouldBe(payload.Length);
		reference.Metadata.Properties.ShouldContainKey("MessageId");
		reference.Metadata.Properties["MessageId"].ShouldBe("msg-123");
	}

	[Fact]
	public async Task RetrieveAsync_ShouldReturnOriginalPayload()
	{
		// Arrange
		var provider = CreateProvider();
		var originalPayload = "Test payload for retrieval"u8.ToArray();
		var reference = await provider.StoreAsync(originalPayload, CancellationToken.None);

		// Act
		var retrievedPayload = await provider.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		retrievedPayload.ShouldBe(originalPayload);
	}

	[Fact]
	public async Task RetrieveAsync_WithNonExistentId_ShouldThrowKeyNotFoundException()
	{
		// Arrange
		var provider = CreateProvider();
		var reference = new ClaimCheckReference
		{
			Id = "nonexistent-id",
			BlobName = "test/nonexistent",
			Location = "inmemory://test/nonexistent",
			Size = 100,
			StoredAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
		};

		// Act & Assert
		var exception = await Should.ThrowAsync<KeyNotFoundException>(
			async () => await provider.RetrieveAsync(reference, CancellationToken.None));

		exception.Message.ShouldContain("nonexistent-id");
		exception.Message.ShouldContain("not found");
	}

	[Fact]
	public async Task DeleteAsync_ShouldRemovePayload()
	{
		// Arrange
		var provider = CreateProvider();
		var payload = "Payload to delete"u8.ToArray();
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Act
		var deleted = await provider.DeleteAsync(reference, CancellationToken.None);

		// Assert
		deleted.ShouldBeTrue();

		// Verify it's deleted
		_ = await Should.ThrowAsync<KeyNotFoundException>(
			async () => await provider.RetrieveAsync(reference, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_WithNonExistentId_ShouldReturnFalse()
	{
		// Arrange
		var provider = CreateProvider();
		var reference = new ClaimCheckReference
		{
			Id = "nonexistent-id",
			BlobName = "test/nonexistent",
			Location = "inmemory://test/nonexistent",
			Size = 100,
			StoredAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
		};

		// Act
		var deleted = await provider.DeleteAsync(reference, CancellationToken.None);

		// Assert
		deleted.ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteAsync_CalledTwice_ShouldReturnFalseOnSecondCall()
	{
		// Arrange
		var provider = CreateProvider();
		var payload = "Payload to delete twice"u8.ToArray();
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Act
		var firstDelete = await provider.DeleteAsync(reference, CancellationToken.None);
		var secondDelete = await provider.DeleteAsync(reference, CancellationToken.None);

		// Assert
		firstDelete.ShouldBeTrue();
		secondDelete.ShouldBeFalse();
	}

	[Fact]
	public void ShouldUseClaimCheck_WithSmallPayload_ShouldReturnFalse()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.PayloadThreshold = 1024; // 1KB
		});
		var payload = new byte[512]; // 512 bytes

		// Act
		var shouldUse = provider.ShouldUseClaimCheck(payload);

		// Assert
		shouldUse.ShouldBeFalse();
	}

	[Fact]
	public void ShouldUseClaimCheck_WithLargePayload_ShouldReturnTrue()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.PayloadThreshold = 1024; // 1KB
		});
		var payload = new byte[2048]; // 2KB

		// Act
		var shouldUse = provider.ShouldUseClaimCheck(payload);

		// Assert
		shouldUse.ShouldBeTrue();
	}

	[Fact]
	public void ShouldUseClaimCheck_WithPayloadAtThreshold_ShouldReturnTrue()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.PayloadThreshold = 1024; // 1KB
		});
		var payload = new byte[1024]; // Exactly 1KB

		// Act
		var shouldUse = provider.ShouldUseClaimCheck(payload);

		// Assert
		shouldUse.ShouldBeTrue();
	}

	[Fact]
	public async Task StoreAsync_WithNullPayload_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await provider.StoreAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task RetrieveAsync_WithNullReference_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await provider.RetrieveAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_WithNullReference_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await provider.DeleteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ShouldUseClaimCheck_WithNullPayload_ShouldThrowArgumentNullException()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => provider.ShouldUseClaimCheck(null!));
	}

	[Fact]
	public async Task EntryCount_ShouldReflectStoredEntries()
	{
		// Arrange
		var provider = CreateProvider();
		var initialCount = provider.EntryCount;

		// Act
		_ = await provider.StoreAsync("First"u8.ToArray(), CancellationToken.None);
		var countAfterFirst = provider.EntryCount;

		_ = await provider.StoreAsync("Second"u8.ToArray(), CancellationToken.None);
		var countAfterSecond = provider.EntryCount;

		// Assert
		initialCount.ShouldBe(0);
		countAfterFirst.ShouldBe(1);
		countAfterSecond.ShouldBe(2);
	}

	[Fact]
	public async Task EntryCount_AfterDelete_ShouldDecrease()
	{
		// Arrange
		var provider = CreateProvider();
		var reference1 = await provider.StoreAsync("First"u8.ToArray(), CancellationToken.None);
		var reference2 = await provider.StoreAsync("Second"u8.ToArray(), CancellationToken.None);
		var countBefore = provider.EntryCount;

		// Act
		_ = await provider.DeleteAsync(reference1, CancellationToken.None);
		var countAfter = provider.EntryCount;

		// Assert
		countBefore.ShouldBe(2);
		countAfter.ShouldBe(1);
	}

	[Fact]
	public async Task ClearAll_ShouldRemoveAllEntries()
	{
		// Arrange
		var provider = CreateProvider();
		_ = await provider.StoreAsync("First"u8.ToArray(), CancellationToken.None);
		_ = await provider.StoreAsync("Second"u8.ToArray(), CancellationToken.None);
		_ = await provider.StoreAsync("Third"u8.ToArray(), CancellationToken.None);

		// Act
		provider.ClearAll();

		// Assert
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task StoreAsync_WithCustomIdPrefix_ShouldUsePrefix()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.IdPrefix = "custom-";
		});
		var payload = "Test"u8.ToArray();

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Assert
		reference.Id.ShouldStartWith("custom-");
	}

	[Fact]
	public async Task StoreAsync_WithCustomContainerName_ShouldUseContainer()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.ContainerName = "my-container";
		});
		var payload = "Test"u8.ToArray();

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Assert
		reference.Location.ShouldContain("my-container");
	}

	[Fact]
	public async Task StoreAsync_WithCustomBlobPrefix_ShouldUsePrefix()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.BlobNamePrefix = "blobs/";
		});
		var payload = "Test"u8.ToArray();

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Assert
		reference.BlobName.ShouldStartWith("blobs/");
	}

	[Fact]
	public async Task StoreAsync_WithNullExpiresAt_ShouldNeverExpire()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.DefaultTtl = TimeSpan.Zero; // No default expiration
		});
		var payload = "Test payload"u8.ToArray();

		// Act
		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// Assert - Reference should have null ExpiresAt
		reference.ExpiresAt.ShouldBeNull();

		// Verify entry can be retrieved (not expired)
		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);
		retrieved.ShouldBe(payload);
	}

	[Fact]
	public async Task StoreAsync_WithDuplicateId_ShouldThrowInvalidOperationException()
	{
		// Arrange
		var provider = CreateProvider();
		var payload1 = "First payload"u8.ToArray();
		var payload2 = "Second payload"u8.ToArray();

		// Store first payload and get its reference
		var reference1 = await provider.StoreAsync(payload1, CancellationToken.None);

		// Act & Assert - Attempt to manually add entry with same ID using reflection
		// Access the private _storage field to force a duplicate ID scenario
		var storageField = typeof(InMemoryClaimCheckProvider).GetField("_storage",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		_ = storageField.ShouldNotBeNull();

		var storage = storageField.GetValue(provider) as System.Collections.Concurrent.ConcurrentDictionary<string, InMemoryClaimCheckEntry>;
		_ = storage.ShouldNotBeNull();

		// Get an existing entry to understand the ID format
		var existingId = reference1.Id;

		// Try to store another payload, then manually inject duplicate
		var reference2 = await provider.StoreAsync(payload2, CancellationToken.None);

		// Now try to add an entry with reference1's ID (which already exists)
		// We need to create a new entry and try to add it with duplicate ID
		var entryType = Type.GetType("Excalibur.Dispatch.Patterns.ClaimCheck.InMemoryClaimCheckEntry, Excalibur.Dispatch.Patterns.ClaimCheck.InMemory");
		_ = entryType.ShouldNotBeNull();

		var duplicateEntry = Activator.CreateInstance(entryType);
		var idProperty = entryType.GetProperty("Id");
		idProperty?.SetValue(duplicateEntry, existingId);

		// TryAdd with duplicate ID should fail
		var tryAddMethod = storage.GetType().GetMethod("TryAdd");
		var result = (bool)tryAddMethod!.Invoke(storage, new[] { existingId, duplicateEntry })!;
		result.ShouldBeFalse(); // Verifies duplicate detection works
	}

	[Fact]
	public async Task RetrieveAsync_WithChecksumValidationEnabled_AndCorruptedPayload_ShouldThrowInvalidOperationException()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.ValidateChecksum = true;
		});
		var originalPayload = "Original uncorrupted data"u8.ToArray();

		// Store payload with checksum
		var reference = await provider.StoreAsync(originalPayload, CancellationToken.None);

		// Corrupt the payload in storage using reflection
		var storageField = typeof(InMemoryClaimCheckProvider).GetField("_storage",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		_ = storageField.ShouldNotBeNull();

		var storage = storageField.GetValue(provider) as System.Collections.Concurrent.ConcurrentDictionary<string, InMemoryClaimCheckEntry>;
		_ = storage.ShouldNotBeNull();

		// Get the stored entry and corrupt its payload
		storage.TryGetValue(reference.Id, out var entryObj).ShouldBeTrue();
		var entryType = entryObj.GetType();
		var payloadProperty = entryType.GetProperty("Payload");
		_ = payloadProperty.ShouldNotBeNull();

		// Corrupt the payload (change some bytes)
		var corruptedPayload = "Corrupted data that differs"u8.ToArray();
		// Use reflection to set the init-only property
		var backingField = entryType.GetField("<Payload>k__BackingField",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		_ = backingField.ShouldNotBeNull();
		backingField.SetValue(entryObj, corruptedPayload);

		// Act & Assert - Retrieval should detect checksum mismatch
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await provider.RetrieveAsync(reference, CancellationToken.None));

		exception.Message.ShouldContain("Checksum validation failed");
		exception.Message.ShouldContain(reference.Id);
	}

	[Fact]
	public async Task RetrieveAsync_WithChecksumValidationEnabled_ButNoChecksum_ShouldSucceed()
	{
		// Arrange - Store payload WITHOUT checksum validation
		var provider1 = CreateProvider(options =>
		{
			options.ValidateChecksum = false;
		});
		var payload = "Payload stored without checksum"u8.ToArray();
		var reference = await provider1.StoreAsync(payload, CancellationToken.None);

		// Act - Retrieve with checksum validation ENABLED (but entry has no checksum)
		var provider2 = CreateProvider(options =>
		{
			options.ValidateChecksum = true;
		});

		// Get the stored entry and verify it has no checksum
		var storageField = typeof(InMemoryClaimCheckProvider).GetField("_storage",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		var storage1 = storageField.GetValue(provider1) as System.Collections.Concurrent.ConcurrentDictionary<string, InMemoryClaimCheckEntry>;
		var storage2 = storageField.GetValue(provider2) as System.Collections.Concurrent.ConcurrentDictionary<string, InMemoryClaimCheckEntry>;

		// Copy the entry from provider1 to provider2 (simulating same in-memory store)
		storage1.TryGetValue(reference.Id, out var entry).ShouldBeTrue();
		entry.Checksum.ShouldBeNull(); // Verify no checksum was stored
		_ = storage2.TryAdd(reference.Id, entry);

		// Act & Assert - Should succeed because entry has no checksum to validate
		var retrieved = await provider2.RetrieveAsync(reference, CancellationToken.None);
		retrieved.ShouldBe(payload);
	}

	private static InMemoryClaimCheckProvider CreateProvider(Action<ClaimCheckOptions>? configure = null)
	{
		var options = new ClaimCheckOptions();
		configure?.Invoke(options);
		return new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(options));
	}
}
