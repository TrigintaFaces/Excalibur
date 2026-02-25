// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckMetadata"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckMetadataDepthShould
{
	[Fact]
	public void Defaults_AreNullOrEmpty()
	{
		var metadata = new ClaimCheckMetadata();
		metadata.MessageId.ShouldBeNull();
		metadata.MessageType.ShouldBeNull();
		metadata.ContentType.ShouldBeNull();
		metadata.ContentEncoding.ShouldBeNull();
		metadata.IsCompressed.ShouldBeFalse();
		metadata.OriginalSize.ShouldBeNull();
		metadata.CorrelationId.ShouldBeNull();
		metadata.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void SetMessageId_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { MessageId = "msg-001" };
		metadata.MessageId.ShouldBe("msg-001");
	}

	[Fact]
	public void SetMessageType_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { MessageType = "OrderCreated" };
		metadata.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void SetContentType_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { ContentType = "application/json" };
		metadata.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void SetContentEncoding_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { ContentEncoding = "gzip" };
		metadata.ContentEncoding.ShouldBe("gzip");
	}

	[Fact]
	public void SetIsCompressed_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { IsCompressed = true };
		metadata.IsCompressed.ShouldBeTrue();
	}

	[Fact]
	public void SetOriginalSize_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { OriginalSize = 4096 };
		metadata.OriginalSize.ShouldBe(4096);
	}

	[Fact]
	public void SetCorrelationId_ReturnsSetValue()
	{
		var metadata = new ClaimCheckMetadata { CorrelationId = "corr-xyz" };
		metadata.CorrelationId.ShouldBe("corr-xyz");
	}

	[Fact]
	public void Properties_SupportAddAndRetrieve()
	{
		var metadata = new ClaimCheckMetadata();
		metadata.Properties["key1"] = "value1";
		metadata.Properties["key2"] = "value2";

		metadata.Properties.Count.ShouldBe(2);
		metadata.Properties["key1"].ShouldBe("value1");
		metadata.Properties["key2"].ShouldBe("value2");
	}

	[Fact]
	public void Properties_AreInitialized_ViaCollectionInitializer()
	{
		var metadata = new ClaimCheckMetadata
		{
			Properties =
			{
				["OrderId"] = "order-123",
				["TenantId"] = "tenant-456",
			},
		};

		metadata.Properties.Count.ShouldBe(2);
		metadata.Properties["OrderId"].ShouldBe("order-123");
	}

	[Fact]
	public void AllProperties_CanBeSetSimultaneously()
	{
		var metadata = new ClaimCheckMetadata
		{
			MessageId = "mid-1",
			MessageType = "TestEvent",
			ContentType = "application/octet-stream",
			ContentEncoding = "deflate",
			IsCompressed = true,
			OriginalSize = 8192,
			CorrelationId = "corr-full",
		};

		metadata.MessageId.ShouldBe("mid-1");
		metadata.MessageType.ShouldBe("TestEvent");
		metadata.ContentType.ShouldBe("application/octet-stream");
		metadata.ContentEncoding.ShouldBe("deflate");
		metadata.IsCompressed.ShouldBeTrue();
		metadata.OriginalSize.ShouldBe(8192);
		metadata.CorrelationId.ShouldBe("corr-full");
	}
}
