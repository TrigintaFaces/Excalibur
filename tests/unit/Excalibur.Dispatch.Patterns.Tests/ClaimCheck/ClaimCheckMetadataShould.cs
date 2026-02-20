// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckMetadata"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class ClaimCheckMetadataShould
{
	[Fact]
	public void HaveNullMessageId_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.MessageId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMessageType_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.MessageType.ShouldBeNull();
	}

	[Fact]
	public void HaveNullContentType_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.ContentType.ShouldBeNull();
	}

	[Fact]
	public void HaveNullContentEncoding_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.ContentEncoding.ShouldBeNull();
	}

	[Fact]
	public void HaveFalseIsCompressed_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.IsCompressed.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullOriginalSize_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.OriginalSize.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyProperties_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.Properties.ShouldNotBeNull();
		metadata.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void HaveNullCorrelationId_ByDefault()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata();

		// Assert
		metadata.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMessageId()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.MessageId = "msg-12345";

		// Assert
		metadata.MessageId.ShouldBe("msg-12345");
	}

	[Fact]
	public void AllowSettingMessageType()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.MessageType = "OrderCreatedEvent";

		// Assert
		metadata.MessageType.ShouldBe("OrderCreatedEvent");
	}

	[Fact]
	public void AllowSettingContentType()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.ContentType = "application/json";

		// Assert
		metadata.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void AllowSettingContentEncoding()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.ContentEncoding = "gzip";

		// Assert
		metadata.ContentEncoding.ShouldBe("gzip");
	}

	[Fact]
	public void AllowSettingIsCompressed()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.IsCompressed = true;

		// Assert
		metadata.IsCompressed.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingOriginalSize()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.OriginalSize = 4096;

		// Assert
		metadata.OriginalSize.ShouldBe(4096);
	}

	[Fact]
	public void AllowAddingProperties()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.Properties["source"] = "api-gateway";
		metadata.Properties["version"] = "1.0";

		// Assert
		metadata.Properties.Count.ShouldBe(2);
		metadata.Properties["source"].ShouldBe("api-gateway");
		metadata.Properties["version"].ShouldBe("1.0");
	}

	[Fact]
	public void AllowSettingCorrelationId()
	{
		// Arrange
		var metadata = new ClaimCheckMetadata();

		// Act
		metadata.CorrelationId = "corr-abc-123";

		// Assert
		metadata.CorrelationId.ShouldBe("corr-abc-123");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var metadata = new ClaimCheckMetadata
		{
			MessageId = "msg-unique",
			MessageType = "PaymentProcessedEvent",
			ContentType = "application/octet-stream",
			ContentEncoding = "br",
			IsCompressed = true,
			OriginalSize = 8192,
			CorrelationId = "corr-xyz",
		};
		metadata.Properties["tenant"] = "acme-corp";
		metadata.Properties["priority"] = "high";

		// Assert
		metadata.MessageId.ShouldBe("msg-unique");
		metadata.MessageType.ShouldBe("PaymentProcessedEvent");
		metadata.ContentType.ShouldBe("application/octet-stream");
		metadata.ContentEncoding.ShouldBe("br");
		metadata.IsCompressed.ShouldBeTrue();
		metadata.OriginalSize.ShouldBe(8192);
		metadata.CorrelationId.ShouldBe("corr-xyz");
		metadata.Properties["tenant"].ShouldBe("acme-corp");
		metadata.Properties["priority"].ShouldBe("high");
	}
}
