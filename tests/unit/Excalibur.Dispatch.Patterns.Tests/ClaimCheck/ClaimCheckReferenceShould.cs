// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckReference"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class ClaimCheckReferenceShould
{
	[Fact]
	public void HaveEmptyId_ByDefault()
	{
		// Arrange & Act
		var reference = new ClaimCheckReference();

		// Assert
		reference.Id.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyBlobName_ByDefault()
	{
		// Arrange & Act
		var reference = new ClaimCheckReference();

		// Assert
		reference.BlobName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyLocation_ByDefault()
	{
		// Arrange & Act
		var reference = new ClaimCheckReference();

		// Assert
		reference.Location.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveZeroSize_ByDefault()
	{
		// Arrange & Act
		var reference = new ClaimCheckReference();

		// Assert
		reference.Size.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultStoredAt_ByDefault()
	{
		// Arrange & Act
		var reference = new ClaimCheckReference();

		// Assert
		reference.StoredAt.ShouldBe(default);
	}

	[Fact]
	public void HaveNullExpiresAt_ByDefault()
	{
		// Arrange & Act
		var reference = new ClaimCheckReference();

		// Assert
		reference.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMetadata_ByDefault()
	{
		// Arrange & Act
		var reference = new ClaimCheckReference();

		// Assert
		reference.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingId()
	{
		// Arrange
		var reference = new ClaimCheckReference();

		// Act
		reference.Id = "cc-12345";

		// Assert
		reference.Id.ShouldBe("cc-12345");
	}

	[Fact]
	public void AllowSettingBlobName()
	{
		// Arrange
		var reference = new ClaimCheckReference();

		// Act
		reference.BlobName = "claims/2024/01/payload-xyz.json";

		// Assert
		reference.BlobName.ShouldBe("claims/2024/01/payload-xyz.json");
	}

	[Fact]
	public void AllowSettingLocation()
	{
		// Arrange
		var reference = new ClaimCheckReference();

		// Act
		reference.Location = "https://storage.blob.core.windows.net/claims/payload.json";

		// Assert
		reference.Location.ShouldBe("https://storage.blob.core.windows.net/claims/payload.json");
	}

	[Fact]
	public void AllowSettingSize()
	{
		// Arrange
		var reference = new ClaimCheckReference();

		// Act
		reference.Size = 1024 * 1024; // 1MB

		// Assert
		reference.Size.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void AllowSettingStoredAt()
	{
		// Arrange
		var reference = new ClaimCheckReference();
		var storedAt = DateTimeOffset.UtcNow;

		// Act
		reference.StoredAt = storedAt;

		// Assert
		reference.StoredAt.ShouldBe(storedAt);
	}

	[Fact]
	public void AllowSettingExpiresAt()
	{
		// Arrange
		var reference = new ClaimCheckReference();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		// Act
		reference.ExpiresAt = expiresAt;

		// Assert
		reference.ExpiresAt.ShouldBe(expiresAt);
	}

	[Fact]
	public void AllowSettingMetadata()
	{
		// Arrange
		var reference = new ClaimCheckReference();
		var metadata = new ClaimCheckMetadata { MessageId = "msg-123" };

		// Act
		reference.Metadata = metadata;

		// Assert
		reference.Metadata.ShouldNotBeNull();
		reference.Metadata.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var storedAt = DateTimeOffset.UtcNow;
		var expiresAt = storedAt.AddDays(7);

		// Act
		var reference = new ClaimCheckReference
		{
			Id = "cc-unique-id",
			BlobName = "claims/payload.bin",
			Location = "https://storage.example.com/claims/payload.bin",
			Size = 2048,
			StoredAt = storedAt,
			ExpiresAt = expiresAt,
			Metadata = new ClaimCheckMetadata
			{
				MessageId = "msg-456",
				MessageType = "OrderCommand",
				ContentType = "application/json",
				IsCompressed = true,
				OriginalSize = 4096,
			},
		};

		// Assert
		reference.Id.ShouldBe("cc-unique-id");
		reference.BlobName.ShouldBe("claims/payload.bin");
		reference.Location.ShouldContain("storage.example.com");
		reference.Size.ShouldBe(2048);
		reference.StoredAt.ShouldBe(storedAt);
		reference.ExpiresAt.ShouldBe(expiresAt);
		reference.Metadata.ShouldNotBeNull();
		reference.Metadata.MessageId.ShouldBe("msg-456");
		reference.Metadata.IsCompressed.ShouldBeTrue();
	}
}
