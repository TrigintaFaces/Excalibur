// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckReference"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckReferenceDepthShould
{
	[Fact]
	public void Defaults_AreEmpty()
	{
		var reference = new ClaimCheckReference();
		reference.Id.ShouldBe(string.Empty);
		reference.BlobName.ShouldBe(string.Empty);
		reference.Location.ShouldBe(string.Empty);
		reference.Size.ShouldBe(0);
		reference.StoredAt.ShouldBe(default);
		reference.ExpiresAt.ShouldBeNull();
		reference.Metadata.ShouldBeNull();
	}

	[Fact]
	public void SetId_ReturnsSetValue()
	{
		var reference = new ClaimCheckReference { Id = "cc-abc123" };
		reference.Id.ShouldBe("cc-abc123");
	}

	[Fact]
	public void SetBlobName_ReturnsSetValue()
	{
		var reference = new ClaimCheckReference { BlobName = "2026/02/14/cc-abc" };
		reference.BlobName.ShouldBe("2026/02/14/cc-abc");
	}

	[Fact]
	public void SetLocation_ReturnsSetValue()
	{
		var reference = new ClaimCheckReference { Location = "inmemory://container/cc-abc" };
		reference.Location.ShouldBe("inmemory://container/cc-abc");
	}

	[Fact]
	public void SetSize_ReturnsSetValue()
	{
		var reference = new ClaimCheckReference { Size = 1024 };
		reference.Size.ShouldBe(1024);
	}

	[Fact]
	public void SetStoredAt_ReturnsSetValue()
	{
		var now = DateTimeOffset.UtcNow;
		var reference = new ClaimCheckReference { StoredAt = now };
		reference.StoredAt.ShouldBe(now);
	}

	[Fact]
	public void SetExpiresAt_ReturnsSetValue()
	{
		var future = DateTimeOffset.UtcNow.AddDays(7);
		var reference = new ClaimCheckReference { ExpiresAt = future };
		reference.ExpiresAt.ShouldBe(future);
	}

	[Fact]
	public void SetMetadata_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { ContentType = "application/json" };
		var reference = new ClaimCheckReference { Metadata = metadata };
		reference.Metadata.ShouldBeSameAs(metadata);
		reference.Metadata.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void AllProperties_CanBeSetSimultaneously()
	{
		var now = DateTimeOffset.UtcNow;
		var metadata = new ClaimCheckMetadata { MessageType = "TestMsg" };
		var reference = new ClaimCheckReference
		{
			Id = "cc-full",
			BlobName = "path/to/blob",
			Location = "https://store/blob",
			Size = 2048,
			StoredAt = now,
			ExpiresAt = now.AddHours(24),
			Metadata = metadata,
		};

		reference.Id.ShouldBe("cc-full");
		reference.BlobName.ShouldBe("path/to/blob");
		reference.Location.ShouldBe("https://store/blob");
		reference.Size.ShouldBe(2048);
		reference.StoredAt.ShouldBe(now);
		reference.ExpiresAt.ShouldBe(now.AddHours(24));
		reference.Metadata.ShouldBeSameAs(metadata);
	}
}
